using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

public class ComposedSelectorTests {
	// === Helpers ===

	// xUnit may set a SyncContext on the test thread; Store captures it on
	// construction and would Post notifications to a deferred queue, breaking
	// synchronous asserts. Clear it first so callbacks fire inline.
	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	private static void RegisterBothFeatures(Store store) {
		store.AddFeature<TestState>(new TestFeature());
		store.AddFeature<SecondaryState>(new SecondaryFeature());
	}

	// === Memoization ===

	[Fact]
	public void Select_WithNoStateChange_RunsCombinerOnce() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var invocations = 0;
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		IComposedSelector<CombinedProjection> composed = store.Select<TestState, SecondaryState, int, string, CombinedProjection>(
			counterSel, labelSel, (c, l) => {
				invocations++;
				return new CombinedProjection(c, l);
			});

		var first = composed.Select();
		var second = composed.Select();

		Assert.Equal(1, invocations);
		Assert.Same(first, second);
	}

	[Fact]
	public void Select_AfterFirstSliceChanges_RecomputesResult() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var invocations = 0;
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select<TestState, SecondaryState, int, string, string>(
			counterSel, labelSel, (c, l) => {
				invocations++;
				return $"{l}={c}";
			});

		var first = composed.Select();
		dispatcher.Dispatch(new TestActions.IncrementBy(7));
		var second = composed.Select();

		Assert.Equal(2, invocations);
		Assert.Equal("=0", first);
		Assert.Equal("=7", second);
	}

	[Fact]
	public void Select_AfterSecondSliceChanges_RecomputesResult() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var invocations = 0;
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select<TestState, SecondaryState, int, string, string>(
			counterSel, labelSel, (c, l) => {
				invocations++;
				return $"{l}={c}";
			});

		var first = composed.Select();
		dispatcher.Dispatch(new SecondaryActions.SetLabel("hello"));
		var second = composed.Select();

		Assert.Equal(2, invocations);
		Assert.Equal("=0", first);
		Assert.Equal("hello=0", second);
	}

	[Fact]
	public void Select_AfterBothSlicesChange_RecomputesResultOnce() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var invocations = 0;
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select<TestState, SecondaryState, int, string, string>(
			counterSel, labelSel, (c, l) => {
				invocations++;
				return $"{l}={c}";
			});

		composed.Select();
		dispatcher.Dispatch(new TestActions.IncrementBy(3));
		dispatcher.Dispatch(new SecondaryActions.SetLabel("hi"));
		var afterBoth = composed.Select();

		Assert.Equal(2, invocations);
		Assert.Equal("hi=3", afterBoth);
	}

	[Fact]
	public void Select_WhenInnerOutputsUnchanged_ShortCircuits() {
		// SetName mutates Name only; the inner counter selector projects s => s.Counter,
		// so its output stays equal across the dispatch and the composed combiner short-circuits.
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var invocations = 0;
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select<TestState, SecondaryState, int, string, string>(
			counterSel, labelSel, (c, l) => {
				invocations++;
				return $"{l}={c}";
			});

		composed.Select();
		dispatcher.Dispatch(new TestActions.SetName("renamed"));
		composed.Select();

		Assert.Equal(1, invocations);
	}

	// === Cross-feature happy path ===

	[Fact]
	public void Select_AcrossTwoFeatures_ProjectsCombinedValue() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);

		// Inference probe: no <> on the composition call — verifies all five generic
		// type parameters infer cleanly from the three concrete arguments.
		var composed = store.Select(counterSel, labelSel, (c, l) => $"{l}={c}");

		Assert.Equal("=0", composed.Select());

		dispatcher.Dispatch(new TestActions.IncrementBy(7));
		dispatcher.Dispatch(new SecondaryActions.SetLabel("hello"));

		Assert.Equal("hello=7", composed.Select());
	}

	// === Failure modes ===

	[Fact]
	public void Select_WithMissingFeature_ThrowsInvalidOperationException() {
		ClearSyncContext();
		// Register only TestFeature — SecondaryFeature is intentionally absent.
		TestServices.BuildWithStoreAndFeatures(out var store, out _,
			s => s.AddFeature<TestState>(new TestFeature()));
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select(counterSel, labelSel, (c, l) => $"{l}={c}");

		var ex = Assert.Throws<InvalidOperationException>(() => composed.Select());
		Assert.Contains(typeof(SecondaryState).FullName!, ex.Message);
	}

	// === LastResult ===

	[Fact]
	public void LastResult_BeforeAnySelect_ThrowsInvalidOperationException() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select(counterSel, labelSel, (c, l) => $"{l}={c}");

		var ex = Assert.Throws<InvalidOperationException>(() => composed.LastResult);
		Assert.Equal("No projection has been computed yet. Call Select() first.", ex.Message);
	}

	[Fact]
	public void LastResult_AfterSelect_ReflectsCachedResult() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out var dispatcher, RegisterBothFeatures);
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select(counterSel, labelSel, (c, l) => $"{l}={c}");

		dispatcher.Dispatch(new TestActions.IncrementBy(5));
		dispatcher.Dispatch(new SecondaryActions.SetLabel("v"));
		composed.Select();

		Assert.Equal("v=5", composed.LastResult);
	}

	// === Reset ===

	[Fact]
	public void Reset_AfterCachedSelect_ForcesNextSelectToRecompute() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var invocations = 0;
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select<TestState, SecondaryState, int, string, string>(
			counterSel, labelSel, (c, l) => {
				invocations++;
				return $"{l}={c}";
			});

		composed.Select();
		composed.Reset();
		composed.Select();

		Assert.Equal(2, invocations);
	}

	[Fact]
	public void Reset_OnComposedSelector_DoesNotResetInnerSelectors() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select(counterSel, labelSel, (c, l) => $"{l}={c}");

		composed.Select();
		var counterCachedBefore = counterSel.LastResult;
		var labelCachedBefore = labelSel.LastResult;

		composed.Reset();

		// Inner selectors still hold their pre-Reset cache — only the composed layer cleared.
		Assert.Equal(counterCachedBefore, counterSel.LastResult);
		Assert.Equal(labelCachedBefore, labelSel.LastResult);
	}

	// === Null validation ===

	[Fact]
	public void Select_NullSelectorA_ThrowsArgumentNullException() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);

		Assert.Throws<ArgumentNullException>(() =>
			store.Select<TestState, SecondaryState, int, string, string>(
				null!, labelSel, (c, l) => $"{l}={c}")
		);
	}

	[Fact]
	public void Select_NullSelectorB_ThrowsArgumentNullException() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var counterSel = store.Select<TestState, int>(s => s.Counter);

		Assert.Throws<ArgumentNullException>(() =>
			store.Select<TestState, SecondaryState, int, string, string>(
				counterSel, null!, (c, l) => $"{l}={c}")
		);
	}

	[Fact]
	public void Select_NullCombiner_ThrowsArgumentNullException() {
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);

		Assert.Throws<ArgumentNullException>(() =>
			store.Select<TestState, SecondaryState, int, string, string>(
				counterSel, labelSel, null!)
		);
	}

	// === Concurrency ===

	[Fact]
	public void Select_FromMultipleThreads_DoesNotCorruptCache() {
		// Per-instance Lock guards the (lastA, lastB, lastResult, hasResult) quadruple
		// under concurrent Select() reads; Parallel.For with Interlocked-incremented
		// counter detects torn writes or lost updates without Barrier or Thread.Sleep.
		ClearSyncContext();
		TestServices.BuildWithStoreAndFeatures(out var store, out _, RegisterBothFeatures);
		var invocations = 0;
		var counterSel = store.Select<TestState, int>(s => s.Counter);
		var labelSel = store.Select<SecondaryState, string>(s => s.Label);
		var composed = store.Select<TestState, SecondaryState, int, string, string>(
			counterSel, labelSel, (c, l) => {
				Interlocked.Increment(ref invocations);
				return $"{l}={c}";
			});

		var ex = Record.Exception(() =>
			Parallel.For(0, 100, _ => composed.Select())
		);

		Assert.Null(ex);
		Assert.Equal("=0", composed.LastResult);
		Assert.InRange(invocations, 1, 100);
	}

	// === Inline fixtures (private nested) ===

	private record CombinedProjection(int Counter, string Label);
}
