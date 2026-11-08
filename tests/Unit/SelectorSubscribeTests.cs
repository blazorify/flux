using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

public class SelectorSubscribeTests {
	// xUnit may set a SyncContext on the test thread; Store captures it on construction
	// and would Post notifications to a deferred queue, breaking synchronous asserts.
	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	// === Single-state Subscribe (ISelector<TState, TResult>) ===

	[Fact]
	public void Subscribe_OnInitialCall_ReplaysCurrentProjectedValue() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		store.AddFeature<TestState>(new TestFeature());
		var selector = store.Select<TestState, int>(s => s.Counter);
		var invocations = 0;
		var last = -1;

		using var sub = store.Subscribe<TestState, int>(selector, value => {
			Interlocked.Increment(ref invocations);
			last = value;
		});

		Assert.Equal(1, invocations);
		Assert.Equal(0, last);
	}

	[Fact]
	public void Subscribe_AfterDispatchChangingProjection_FiresCallback() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out var dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		var selector = store.Select<TestState, int>(s => s.Counter);
		var invocations = 0;
		var last = -1;

		using var sub = store.Subscribe<TestState, int>(selector, value => {
			Interlocked.Increment(ref invocations);
			last = value;
		});
		// Reset the replay invocation so we observe only post-dispatch ticks.
		invocations = 0;

		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(1, invocations);
		Assert.Equal(1, last);
	}

	[Fact]
	public void Subscribe_AfterDispatchPreservingProjection_DoesNotFireCallback() {
		// Record projection so cached-reference identity is meaningful;
		// ReferenceEquals on boxed value types always returns false and would false-fire here.
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out var dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		var selector = store.Select<TestState, CounterProjection>(s => new CounterProjection(s.Counter));
		var invocations = 0;

		using var sub = store.Subscribe<TestState, CounterProjection>(selector, _ => {
			Interlocked.Increment(ref invocations);
		});
		invocations = 0;

		// IncrementBy(0) mutates state (new TestState instance) but keeps the structural projection equal,
		// so MemoizedSelector returns the cached CounterProjection reference and ReferenceEquals skips the callback.
		dispatcher.Dispatch(new TestActions.IncrementBy(0));

		Assert.Equal(0, invocations);
	}

	[Fact]
	public void Subscribe_BeforeFeatureRegistered_DoesNotReplayAndDoesNotThrow() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out var dispatcher);
		var selector = store.Select<TestState, int>(s => s.Counter);
		var invocations = 0;

		var ex = Record.Exception(() => {
			using var sub = store.Subscribe<TestState, int>(selector, _ => {
				Interlocked.Increment(ref invocations);
			});

			Assert.Null(Record.Exception(() => { /* no-op */ }));
			Assert.Equal(0, invocations);

			store.AddFeature<TestState>(new TestFeature());
			dispatcher.Dispatch(new TestActions.Increment());

			Assert.Equal(1, invocations);
		});

		Assert.Null(ex);
	}

	[Fact]
	public void Subscribe_DisposeRemovesFromSubscriberListAndPrunesEntry() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		store.AddFeature<TestState>(new TestFeature());
		var selector = store.Select<TestState, int>(s => s.Counter);

		var sub = store.Subscribe<TestState, int>(selector, _ => { });
		Assert.True(store.HasSubscriberEntry(typeof(TestState)));

		sub.Dispose();

		Assert.False(store.HasSubscriberEntry(typeof(TestState)));
	}

	[Fact]
	public void Subscribe_NullArguments_ThrowArgumentNullException() {
		// Two null-arg shapes consolidated into one [Fact]: the two generic-signature
		// inferences differ (null selector requires explicit TResult; null callback
		// needs an Action<int> cast to disambiguate the sync vs async overloads).
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var selector = store.Select<TestState, int>(s => s.Counter);

		Assert.Throws<ArgumentNullException>(() =>
			store.Subscribe<TestState, int>(null!, _ => { }));

		Assert.Throws<ArgumentNullException>(() =>
			store.Subscribe<TestState, int>(selector, (Action<int>)null!));
	}

	[Fact]
	public async Task Subscribe_AsyncCallback_AwaitsBeforeNextNotify() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out var dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		var selector = store.Select<TestState, int>(s => s.Counter);
		var invocations = 0;

		using var sub = store.Subscribe<TestState, int>(selector, async _ => {
			await Task.Yield();
			Interlocked.Increment(ref invocations);
		});
		// Drain the initial replay's fire-and-forget Task.
		await Task.Delay(50);
		var beforeDispatch = Volatile.Read(ref invocations);
		Assert.InRange(beforeDispatch, 1, 1);

		dispatcher.Dispatch(new TestActions.Increment());
		await Task.Delay(50);
		var afterDispatch = Volatile.Read(ref invocations);
		Assert.InRange(afterDispatch, 2, 2);
	}

	[Fact]
	public void Subscribe_FromMultipleThreads_DoesNotCorruptCallbackList() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		store.AddFeature<TestState>(new TestFeature());
		var selector = store.Select<TestState, int>(s => s.Counter);
		var invocations = 0;

		var ex = Record.Exception(() =>
			Parallel.For(0, 100, _ => {
				var sub = store.Subscribe<TestState, int>(selector, _ => Interlocked.Increment(ref invocations));
				sub.Dispose();
			})
		);

		Assert.Null(ex);
		Assert.False(store.HasSubscriberEntry(typeof(TestState)));
		Assert.InRange(invocations, 100, 200);
	}

	// === Composed Subscribe (IComposedSelector<TResult>) ===

	private static void RegisterBothFeatures(Store store) {
		store.AddFeature<TestState>(new TestFeature());
		store.AddFeature<SecondaryState>(new SecondaryFeature());
	}

	private static IComposedSelector<CombinedProjection> SelectCombined(Store store) {
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		return store.Select(counterSel, labelSel, (c, l) => new CombinedProjection(c, l));
	}

	[Fact]
	public void Subscribe_OnInitialCall_ReplaysComposedProjection() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var composed = SelectCombined(store);

		var invocations = 0;
		CombinedProjection? last = null;
		using var sub = store.Subscribe<CombinedProjection>(composed, view => {
			invocations++;
			last = view;
		});

		Assert.Equal(1, invocations);
		Assert.Equal(new CombinedProjection(0, string.Empty), last);
	}

	[Fact]
	public void Subscribe_AfterDispatchChangingFirstSlice_FiresCallback() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var composed = SelectCombined(store);

		var invocations = 0;
		CombinedProjection? last = null;
		using var sub = store.Subscribe<CombinedProjection>(composed, view => {
			invocations++;
			last = view;
		});
		invocations = 0;

		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(1, invocations);
		Assert.Equal(1, last!.Counter);
	}

	[Fact]
	public void Subscribe_AfterDispatchChangingSecondSlice_FiresCallback() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var composed = SelectCombined(store);

		var invocations = 0;
		CombinedProjection? last = null;
		using var sub = store.Subscribe<CombinedProjection>(composed, view => {
			invocations++;
			last = view;
		});
		invocations = 0;

		dispatcher.Dispatch(new SecondaryActions.SetLabel("changed"));

		Assert.Equal(1, invocations);
		Assert.Equal("changed", last!.Label);
	}

	[Fact]
	public void Subscribe_AfterDispatchPreservingComposedProjection_DoesNotFireCallback() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var composed = SelectCombined(store);

		var invocations = 0;
		using var sub = store.Subscribe<CombinedProjection>(composed, _ => invocations++);
		invocations = 0;

		// Name is not part of the projection, so the composed selector hands back its cached instance.
		dispatcher.Dispatch(new TestActions.SetName("renamed"));

		Assert.Equal(0, invocations);
	}

	[Fact]
	public void Subscribe_BeforeFeatureRegistered_ThrowsInvalidOperationException() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _,
			s => s.AddFeature<TestState>(new TestFeature()));
		var composed = SelectCombined(store);

		var ex = Assert.Throws<InvalidOperationException>(() =>
			store.Subscribe<CombinedProjection>(composed, _ => { }));

		Assert.Contains(typeof(SecondaryState).FullName!, ex.Message);
		Assert.False(store.HasSubscriberEntry(typeof(TestState)));
		Assert.False(store.HasSubscriberEntry(typeof(SecondaryState)));
	}

	[Fact]
	public void Subscribe_DisposeRemovesFromBothSubscriberListsAndPrunesEntries() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var composed = SelectCombined(store);

		var sub = store.Subscribe<CombinedProjection>(composed, _ => { });

		Assert.True(store.HasSubscriberEntry(typeof(TestState)));
		Assert.True(store.HasSubscriberEntry(typeof(SecondaryState)));

		sub.Dispose();

		Assert.False(store.HasSubscriberEntry(typeof(TestState)));
		Assert.False(store.HasSubscriberEntry(typeof(SecondaryState)));
	}

	[Fact]
	public void Subscribe_TwoComposedSubscribers_FireIndependently() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var composed = SelectCombined(store);

		var inv1 = 0;
		var inv2 = 0;
		using var sub1 = store.Subscribe<CombinedProjection>(composed, _ => inv1++);
		using var sub2 = store.Subscribe<CombinedProjection>(composed, _ => inv2++);
		inv1 = 0;
		inv2 = 0;

		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(1, inv1);
		Assert.Equal(1, inv2);
	}

	[Fact]
	public void Subscribe_FromMultipleThreads_DoesNotCorruptComposedCallbackList() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var composed = SelectCombined(store);
		var invocations = 0;

		var ex = Record.Exception(() =>
			Parallel.For(0, 100, _ => {
				var sub = store.Subscribe<CombinedProjection>(composed, _ => Interlocked.Increment(ref invocations));
				sub.Dispose();
			})
		);

		Assert.Null(ex);
		Assert.False(store.HasSubscriberEntry(typeof(TestState)));
		Assert.False(store.HasSubscriberEntry(typeof(SecondaryState)));
		Assert.InRange(invocations, 100, 200);
	}

	[Fact]
	public void Subscribe_DispatchesToBothSlicesOnDifferentThreads_DoNotRunCallbackConcurrently() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var composed = SelectCombined(store);
		var inside = 0;
		var maxInside = 0;
		var firstEntered = new ManualResetEventSlim();

		using var sub = store.Subscribe<CombinedProjection>(composed, view => {
			var now = Interlocked.Increment(ref inside);
			InterlockedMax(ref maxInside, now);
			if (view.Counter == 1) {
				firstEntered.Set();
				Thread.Sleep(200);
			}
			Interlocked.Decrement(ref inside);
		});

		var processor = (IStore)store;
		var first = Task.Run(() => processor.ProcessAction(new TestActions.Increment()));
		Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));
		var second = Task.Run(() => processor.ProcessAction(new SecondaryActions.SetLabel("changed")));
		Task.WaitAll(first, second);

		Assert.Equal(1, maxInside);
	}

	[Fact]
	public async Task Subscribe_AsyncCallback_ReplaysAndFiresOnEitherSlice() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var composed = SelectCombined(store);
		var seen = new ConcurrentQueue<CombinedProjection>();

		using var sub = store.Subscribe<CombinedProjection>(composed, async view => {
			await Task.Yield();
			seen.Enqueue(view);
		});
		dispatcher.Dispatch(new TestActions.Increment());
		dispatcher.Dispatch(new SecondaryActions.SetLabel("changed"));

		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (DateTime.UtcNow < deadline && seen.Count < 3) {
			await Task.Delay(10);
		}

		Assert.Equal(3, seen.Count);
		Assert.Contains(new CombinedProjection(1, "changed"), seen);
	}

	[Fact]
	public void Subscribe_InitialComposedProjectionIsNull_ReplaysNull() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select(counterSel, labelSel, (c, l) => (CombinedProjection?)null);

		var invocations = 0;
		using var sub = store.Subscribe<CombinedProjection?>(composed, _ => invocations++);

		Assert.Equal(1, invocations);
	}

	[Fact]
	public void Subscribe_AsyncCallback_InitialComposedProjectionIsNull_ReplaysNull() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select(counterSel, labelSel, (c, l) => (CombinedProjection?)null);

		var invocations = 0;
		using var sub = store.Subscribe<CombinedProjection?>(composed, _ => {
			invocations++;
			return Task.CompletedTask;
		});

		Assert.Equal(1, invocations);
	}

	[Fact]
	public void Subscribe_AsyncCallback_AfterDispatchPreservingComposedProjection_DoesNotFireCallback() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var composed = SelectCombined(store);

		var invocations = 0;
		using var sub = store.Subscribe<CombinedProjection>(composed, _ => {
			invocations++;
			return Task.CompletedTask;
		});
		invocations = 0;

		dispatcher.Dispatch(new TestActions.SetName("renamed"));

		Assert.Equal(0, invocations);
	}

	[Fact]
	public void Subscribe_Composed_NullArguments_Throw() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var composed = SelectCombined(store);

		Assert.Throws<ArgumentNullException>(() => store.Subscribe<CombinedProjection>(null!, (Action<CombinedProjection>)(_ => { })));
		Assert.Throws<ArgumentNullException>(() => store.Subscribe<CombinedProjection>(null!, (Func<CombinedProjection, Task>)(_ => Task.CompletedTask)));
		Assert.Throws<ArgumentNullException>(() => store.Subscribe(composed, (Action<CombinedProjection>)null!));
		Assert.Throws<ArgumentNullException>(() => store.Subscribe(composed, (Func<CombinedProjection, Task>)null!));
	}

	private static void InterlockedMax(ref int target, int value) {
		int current;
		while (value > (current = Volatile.Read(ref target))) {
			if (Interlocked.CompareExchange(ref target, value, current) == current) {
				return;
			}
		}
	}

	private record CombinedProjection(int Counter, string Label);

	private record CounterProjection(int Value);
}
