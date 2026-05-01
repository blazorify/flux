using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

public class DispatcherTests {
	// === Helpers ===

	// xUnit's SyncContext would make Store post notifications to a deferred queue;
	// clear it so callbacks fire inline and synchronous asserts can see them.
	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	private static Store BuildStoreWithDispatcher(out Dispatcher dispatcher) {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		return store;
	}

	// === Dispatch overloads ===

	[Fact]
	public void Dispatch_WhenTypedActionDispatched_ProcessesAction() {
		var store = BuildStoreWithDispatcher(out var dispatcher);

		dispatcher.Dispatch<TestActions.Increment>();

		Assert.Equal(1, store.GetFeature<TestState>()!.State.Counter);
	}

	[Fact]
	public void Dispatch_WhenActionInstanceDispatched_ProcessesAction() {
		var store = BuildStoreWithDispatcher(out var dispatcher);

		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(1, store.GetFeature<TestState>()!.State.Counter);
	}

	[Fact]
	public void Dispatch_WhenFactoryFunctionDispatched_ProcessesAction() {
		var store = BuildStoreWithDispatcher(out var dispatcher);

		dispatcher.Dispatch(() => new TestActions.IncrementBy(5));

		Assert.Equal(5, store.GetFeature<TestState>()!.State.Counter);
	}

	[Fact]
	public void Dispatch_WhenTransformFunctionDispatched_ProcessesAction() {
		// The transform overload requires `where TAction : new()`, so only payload-less
		// actions qualify; identity `a => a` is the minimal way to exercise the path.
		var store = BuildStoreWithDispatcher(out var dispatcher);

		dispatcher.Dispatch<TestActions.Increment>(a => a);

		Assert.Equal(1, store.GetFeature<TestState>()!.State.Counter);
	}

	[Fact]
	public void Dispatch_WhenIActionDispatched_ProcessesAction() {
		var store = BuildStoreWithDispatcher(out var dispatcher);

		dispatcher.Dispatch((IAction)new TestActions.Increment());

		Assert.Equal(1, store.GetFeature<TestState>()!.State.Counter);
	}

	// === Guards ===

	[Fact]
	public void Dispatch_WhenNullAction_ThrowsArgumentNullException() {
		var _ = BuildStoreWithDispatcher(out var dispatcher);

		Assert.Throws<ArgumentNullException>(() => dispatcher.Dispatch((IAction)null!));
	}

	// === Queue ordering ===

	[Fact]
	public void Dispatch_WhenMultipleActions_ProcessesInOrder() {
		var store = BuildStoreWithDispatcher(out var dispatcher);

		dispatcher.Dispatch(new TestActions.Increment());
		dispatcher.Dispatch(new TestActions.Increment());
		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(3, store.GetFeature<TestState>()!.State.Counter);
	}

	[Fact]
	public void Dispatch_WhenActionUnknown_DoesNothing() {
		// Unregistered has no matching reducer in TestFeature; state must be unchanged.
		var store = BuildStoreWithDispatcher(out var dispatcher);

		dispatcher.Dispatch(new TestActions.Unregistered());

		Assert.Equal(0, store.GetFeature<TestState>()!.State.Counter);
		Assert.Equal(string.Empty, store.GetFeature<TestState>()!.State.Name);
	}

	// === Concurrency ===

	[Fact]
	public void Dispatch_WhenCalledConcurrently_ProcessesAllActions() {
		// 4950 = sum 0..99; addition is commutative, so parallel ordering doesn't change the total.
		var store = BuildStoreWithDispatcher(out var dispatcher);

		Parallel.For(0, 100, i => dispatcher.Dispatch(new TestActions.IncrementBy(i)));

		Assert.Equal(4950, store.GetFeature<TestState>()!.State.Counter);
	}

	[Fact]
	public void Dispatch_WhenConcurrentAndSequential_NoActionsLost() {
		var store = BuildStoreWithDispatcher(out var dispatcher);

		for (var i = 0; i < 50; i++) {
			dispatcher.Dispatch(new TestActions.Increment());
		}

		Parallel.For(0, 50, _ => dispatcher.Dispatch(new TestActions.Increment()));

		Assert.Equal(100, store.GetFeature<TestState>()!.State.Counter);
	}

	// === Error recovery ===

	[Fact]
	public void Dispatch_WhenReducerThrows_ContinuesProcessingRemainingActions() {
		var store = BuildStoreWithDispatcher(out var dispatcher);

		dispatcher.Dispatch(new TestActions.Increment());
		dispatcher.Dispatch(new TestActions.Throw());
		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(2, store.GetFeature<TestState>()!.State.Counter);
	}
}
