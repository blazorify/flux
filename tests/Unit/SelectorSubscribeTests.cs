using System;
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

	private record CounterProjection(int Value);
}
