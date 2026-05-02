using System.Collections.Concurrent;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

public class ParameterizedSelectorTests {
	// === Helpers ===

	// xUnit may set a SyncContext on the test thread; Store captures it on
	// construction and would Post notifications to a deferred queue, breaking
	// synchronous asserts. Clear it first so callbacks fire inline.
	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	// Lifted to a static field so two distinct call sites pass the SAME delegate
	// reference — required for cross-call-site cache dedupe (single MethodInfo, null Target).
	private static readonly Func<TestState, int, int> SharedProjector =
		(state, offset) => state.Counter + offset;

	// === Cache identity (same key → same instance) ===

	[Fact]
	public void Select_WithSameProjectorAndArg_ReturnsSameSelectorInstance() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);

		var first = store.Select<TestState, int, int>(SharedProjector, 5);
		var second = store.Select<TestState, int, int>(SharedProjector, 5);

		Assert.Same(first, second);
	}

	// === Per-arg dedup ===

	[Fact]
	public void Select_WithSameProjectorDifferentArgs_ReturnsDistinctSelectors() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);

		var byFive = store.Select<TestState, int, int>(SharedProjector, 5);
		var bySix = store.Select<TestState, int, int>(SharedProjector, 6);

		Assert.NotSame(byFive, bySix);
	}

	// === Projector identity ===

	[Fact]
	public void Select_WithDifferentProjectorsSameArg_ReturnsDistinctSelectors() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		// Two textually identical lambdas at distinct call sites compile to distinct
		// compiler-generated static methods — different MethodInfo, different cache slot.
		Func<TestState, int, int> projectorA = (state, offset) => state.Counter + offset;
		Func<TestState, int, int> projectorB = (state, offset) => state.Counter + offset;

		var fromA = store.Select<TestState, int, int>(projectorA, 5);
		var fromB = store.Select<TestState, int, int>(projectorB, 5);

		Assert.NotSame(fromA, fromB);
	}

	[Fact]
	public void Select_WithSharedStaticProjector_DedupesAcrossCallSites() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);

		// Both call sites pass the same delegate reference (the static field) —
		// single MethodInfo + null Target → single cache slot.
		var firstCallSite = store.Select<TestState, int, int>(SharedProjector, 7);
		var secondCallSite = store.Select<TestState, int, int>(SharedProjector, 7);

		Assert.Same(firstCallSite, secondCallSite);
	}

	// === Custom argComparer ===

	[Fact]
	public void Select_WithCustomArgComparer_DedupesByComparerSemantics() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		Func<TestState, string, int> projector = (state, key) => state.Counter + key.Length;

		var lower = store.Select<TestState, string, int>(projector, "hello", StringComparer.OrdinalIgnoreCase);
		var upper = store.Select<TestState, string, int>(projector, "HELLO", StringComparer.OrdinalIgnoreCase);

		Assert.Same(lower, upper);
	}

	// === Null TArg ===

	[Fact]
	public void Select_WithNullArg_TreatsNullAsValidDistinctKey() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		Func<TestState, string?, int> projector = (state, name) => name is null ? 0 : state.Counter;

		var nullCall1 = store.Select<TestState, string?, int>(projector, null);
		var nullCall2 = store.Select<TestState, string?, int>(projector, null);
		var nonNullCall = store.Select<TestState, string?, int>(projector, "x");

		Assert.Same(nullCall1, nullCall2);
		Assert.NotSame(nullCall1, nonNullCall);
	}

	// === Per-arg projector invocation count ===

	[Fact]
	public void Select_PerArgProjectorRunsOncePerArg() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var invocations = 0;
		Func<TestState, int, int> projector = (state, offset) => {
			invocations++;
			return state.Counter + offset;
		};

		var selector = store.Select<TestState, int, int>(projector, 5);
		var state = new TestState { Counter = 10 };
		selector.Select(state);
		selector.Select(state);

		Assert.Equal(1, invocations);
	}

	// === Reset preserves identity ===

	[Fact]
	public void Reset_OnCachedSelector_PreservesCacheIdentity() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var invocations = 0;
		Func<TestState, int, int> projector = (state, offset) => {
			invocations++;
			return state.Counter + offset;
		};

		var first = store.Select<TestState, int, int>(projector, 5);
		var state = new TestState { Counter = 10 };
		first.Select(state);
		first.Select(state);

		first.Reset();
		var refetched = store.Select<TestState, int, int>(projector, 5);
		refetched.Select(state);

		Assert.Same(first, refetched);
		Assert.Equal(2, invocations);
	}

	// === Null validation ===

	[Fact]
	public void Select_NullProjector_ThrowsArgumentNullException() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);

		Assert.Throws<ArgumentNullException>(() =>
			store.Select<TestState, int, int>(null!, 5)
		);
		Assert.Throws<ArgumentNullException>(() =>
			store.Select<TestState, int, int>(null!, 5, EqualityComparer<int>.Default)
		);
	}

	[Fact]
	public void Select_NullArgComparer_ThrowsArgumentNullException() {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);

		Assert.Throws<ArgumentNullException>(() =>
			store.Select<TestState, int, int>(SharedProjector, 5, null!)
		);
	}

	// === Concurrency (mixed args) ===

	[Fact]
	public void Select_FromMultipleThreadsWithMixedArgs_DoesNotCorruptCache() {
		// Outer cache lookup is thread-safe; mixed args should hit exactly four cache
		// slots and each per-arg MemoizedSelector's per-instance Lock keeps the inner
		// (lastInput, lastOutput, hasResult) triple consistent under contention.
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out _);
		var invocations = 0;
		Func<TestState, int, int> projector = (state, offset) => {
			Interlocked.Increment(ref invocations);
			return state.Counter + offset;
		};
		var state = new TestState { Counter = 10 };
		var instances = new ConcurrentBag<ISelector<TestState, int>>();

		var ex = Record.Exception(() => Parallel.For(0, 100, i => {
			var sel = store.Select<TestState, int, int>(projector, i % 4);
			sel.Select(state);
			instances.Add(sel);
		}));

		Assert.Null(ex);
		Assert.Equal(4, instances.Distinct().Count());
		Assert.InRange(invocations, 4, 100);
	}
}
