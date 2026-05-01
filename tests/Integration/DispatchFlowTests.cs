using Blazorify.Flux.Core;
using Blazorify.Flux.Tests.Integration.Fixtures;
using Xunit;

namespace Blazorify.Flux.Tests.Integration;

public class DispatchFlowTests {

	// xUnit v2's AsyncTestSyncContext would defer Store.NotifySubscribers Posts past synchronous Asserts; null Current makes notifications fire inline.
	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	private static Store BuildStoreWithDispatcher(out Dispatcher dispatcher) {
		ClearSyncContext();
		IntegrationTestServices.BuildWithStore(out var store, out dispatcher);
		store.AddFeature<IntegrationTestState>(new IntegrationTestFeature());
		return store;
	}

	[Fact]
	public void Dispatch_WhenIncrementDispatched_ReducerMutatesState() {
		var store = BuildStoreWithDispatcher(out var dispatcher);

		dispatcher.Dispatch(new IntegrationTestActions.Increment());

		Assert.Equal(1, store.GetFeature<IntegrationTestState>()!.State.Counter);
	}

	[Fact]
	public void Subscribe_AfterDispatch_FiresCallbackWithReplayThenPostDispatchState() {
		var store = BuildStoreWithDispatcher(out var dispatcher);
		var observed = new List<int>();
		using var sub = store.Subscribe<IntegrationTestState>(s => observed.Add(s.Counter));

		dispatcher.Dispatch(new IntegrationTestActions.Increment());

		Assert.Equal(2, observed.Count);
		Assert.Equal(0, observed[0]);
		Assert.Equal(1, observed[1]);
	}

	[Fact]
	public async Task Dispatch_WhenAsyncEffectRuns_FollowUpActionUpdatesState() {
		// Effect runs on a thread-pool thread (null SyncContext) so the subscriber callback fires inline and completes the TCS.
		ClearSyncContext();
		var store = BuildStoreWithDispatcher(out var dispatcher);
		var completion = new TaskCompletionSource<IntegrationTestState>();
		using var sub = store.Subscribe<IntegrationTestState>(s => {
			if (s.LastEffectMarker == "AsyncWorkCompleted") {
				completion.TrySetResult(s);
			}
		});

		dispatcher.Dispatch(new IntegrationTestActions.StartAsyncWork());

		var finalState = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
		Assert.Equal(10, finalState.Counter);
		Assert.Equal("AsyncWorkCompleted", finalState.LastEffectMarker);
	}

	[Fact]
	public void Dispatch_WithMultipleSequentialActions_AccumulatesStateChanges() {
		var store = BuildStoreWithDispatcher(out var dispatcher);

		dispatcher.Dispatch(new IntegrationTestActions.Increment());
		dispatcher.Dispatch(new IntegrationTestActions.Increment());
		dispatcher.Dispatch(new IntegrationTestActions.IncrementBy(5));
		dispatcher.Dispatch(new IntegrationTestActions.SetName("done"));

		var state = store.GetFeature<IntegrationTestState>()!.State;
		Assert.Equal(7, state.Counter);
		Assert.Equal("done", state.Name);
	}

	[Fact]
	public void Subscribe_AfterDispose_NoFurtherCallbacksFire() {
		var store = BuildStoreWithDispatcher(out var dispatcher);
		var observed = new List<int>();
		var sub = store.Subscribe<IntegrationTestState>(s => observed.Add(s.Counter));
		var countAfterReplay = observed.Count;

		sub.Dispose();
		dispatcher.Dispatch(new IntegrationTestActions.Increment());

		Assert.Equal(countAfterReplay, observed.Count);
		Assert.Equal(1, store.GetFeature<IntegrationTestState>()!.State.Counter);
	}
}
