using Blazorify.Flux.Core;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

public class SelectorTests {
	// === Helpers ===

	// xUnit may set a SyncContext on the test thread; Store captures it on
	// construction and would Post notifications to a deferred queue, breaking
	// synchronous asserts. Clear it first so callbacks fire inline.
	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	// === Memoization (input ref-equality) ===

	[Fact]
	public void Select_CalledTwiceWithSameInput_RunsProjectorOnce() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var invocations = 0;
		var selector = store.Select<TestState, int>(s => {
			invocations++;
			return s.Counter;
		});

		var state = new TestState { Counter = 7 };
		var first = selector.Select(state);
		var second = selector.Select(state);

		Assert.Equal(1, invocations);
		Assert.Equal(7, first);
		Assert.Equal(7, second);
	}

	[Fact]
	public void Select_AfterInputChange_RecomputesProjection() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var invocations = 0;
		var selector = store.Select<TestState, int>(s => {
			invocations++;
			return s.Counter;
		});

		var state1 = new TestState { Counter = 1 };
		var state2 = new TestState { Counter = 2 };
		var first = selector.Select(state1);
		var second = selector.Select(state2);

		Assert.Equal(2, invocations);
		Assert.Equal(1, first);
		Assert.Equal(2, second);
	}

	// === Output short-circuit (reference preservation) ===

	[Fact]
	public void Select_WhenProjectionUnchangedAcrossInputChange_ReturnsCachedReference() {
		// Input ref changed but the projection's structural value didn't —
		// the selector must return the previously cached reference (NOT a new instance).
		// Asserting Assert.Same(int, int) would box and always fail; use a record TResult.
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var invocations = 0;
		var selector = store.Select<TestState, CounterProjection>(s => {
			invocations++;
			return new CounterProjection(s.Counter);
		});

		var state1 = new TestState { Counter = 5, Name = "a" };
		var state2 = state1 with { Name = "b" };

		var first = selector.Select(state1);
		var second = selector.Select(state2);

		Assert.Equal(2, invocations);
		Assert.Same(first, second);
	}

	// === Custom comparers ===

	[Fact]
	public void Select_WithCustomInputComparer_UsesProvidedComparer() {
		// Comparer that always returns true → projector runs exactly once across many calls.
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var invocations = 0;
		var alwaysEqual = EqualityComparer<TestState>.Create((_, _) => true, _ => 0);
		var selector = store.Select<TestState, int>(
			s => {
				invocations++;
				return s.Counter;
			},
			alwaysEqual
		);

		selector.Select(new TestState { Counter = 1 });
		selector.Select(new TestState { Counter = 2 });
		selector.Select(new TestState { Counter = 3 });

		Assert.Equal(1, invocations);
	}

	[Fact]
	public void Select_WithCustomOutputComparer_AppliesShortCircuit() {
		// OrdinalIgnoreCase comparer treats "Hello" and "HELLO" as equal; selector
		// returns the cached reference even though the projector ran for both calls.
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var invocations = 0;
		var selector = store.Select<TestState, string>(
			s => {
				invocations++;
				return s.Name;
			},
			ReferenceEqualityComparer.Instance,
			StringComparer.OrdinalIgnoreCase
		);

		var state1 = new TestState { Name = "Hello" };
		var state2 = new TestState { Name = "HELLO" };
		var first = selector.Select(state1);
		var second = selector.Select(state2);

		Assert.Equal(2, invocations);
		Assert.Same(first, second);
	}

	// === LastResult ===

	[Fact]
	public void LastResult_AfterSelect_ReflectsCachedOutput() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var selector = store.Select<TestState, int>(s => s.Counter);

		selector.Select(new TestState { Counter = 42 });

		Assert.Equal(42, selector.LastResult);
	}

	[Fact]
	public void LastResult_BeforeAnySelect_ThrowsInvalidOperationException() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var selector = store.Select<TestState, int>(s => s.Counter);

		Assert.Throws<InvalidOperationException>(() => _ = selector.LastResult);
	}

	// === Reset ===

	[Fact]
	public void Reset_AfterCachedSelect_ForcesNextSelectToRecompute() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var invocations = 0;
		var selector = store.Select<TestState, int>(s => {
			invocations++;
			return s.Counter;
		});

		var state = new TestState { Counter = 3 };
		selector.Select(state);
		selector.Select(state);

		selector.Reset();
		selector.Select(state);

		Assert.Equal(2, invocations);
	}

	[Fact]
	public void Reset_AfterSelect_LastResultThrowsAgain() {
		// Reset clears hasResult; LastResult should once again behave as "not yet computed".
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var selector = store.Select<TestState, int>(s => s.Counter);
		selector.Select(new TestState { Counter = 1 });

		selector.Reset();

		Assert.Throws<InvalidOperationException>(() => _ = selector.LastResult);
	}

	// === Concurrency ===

	[Fact]
	public void Select_FromMultipleThreads_DoesNotCorruptCache() {
		// Per-instance Lock guards the (lastInput, lastOutput, hasResult) triple under
		// concurrent reads; Parallel.For with a thread-safe counter detects torn writes
		// or lost updates without Barrier or Thread.Sleep.
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var invocations = 0;
		var selector = store.Select<TestState, int>(s => {
			Interlocked.Increment(ref invocations);
			return s.Counter;
		});

		var state = new TestState { Counter = 42 };

		var ex = Record.Exception(() =>
			Parallel.For(0, 100, _ => selector.Select(state))
		);

		Assert.Null(ex);
		Assert.Equal(42, selector.LastResult);
		Assert.InRange(invocations, 1, 100);
	}

	// === Inline fixtures (private nested) ===

	private record CounterProjection(int Value);
}
