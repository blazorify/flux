using Blazorify.Flux.Tests.Integration.Fixtures;
using Xunit;

namespace Blazorify.Flux.Tests.Integration;

public class SyncContextTests {

	// xUnit v2's AsyncTestSyncContext leaves Current non-null on the test thread; tests that assert inline-invocation must null Current before constructing Store.
	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	[Fact]
	public void Notify_WhenSyncContextSet_PostsCallbackToContext() {
		IntegrationTestServices.BuildWithStore(out var store, out var dispatcher);
		store.AddFeature<IntegrationTestState>(new IntegrationTestFeature());

		var recording = new RecordingSyncContext();
		SynchronizationContext.SetSynchronizationContext(recording);
		try {
			using var sub = store.Subscribe<IntegrationTestState>(_ => { });
			var beforeDispatch = recording.Posts.Count;

			dispatcher.Dispatch(new IntegrationTestActions.Increment());

			Assert.True(
				recording.Posts.Count > beforeDispatch,
				"Dispatch should add at least one Post beyond the subscribe-time replay"
			);
		} finally {
			SynchronizationContext.SetSynchronizationContext(null);
		}
	}

	[Fact]
	public void Notify_WhenSyncContextNull_InvokesCallbackInline() {
		ClearSyncContext();
		IntegrationTestServices.BuildWithStore(out var store, out var dispatcher);
		store.AddFeature<IntegrationTestState>(new IntegrationTestFeature());

		var observed = new List<int>();
		using var sub = store.Subscribe<IntegrationTestState>(s => observed.Add(s.Counter));

		dispatcher.Dispatch(new IntegrationTestActions.Increment());

		Assert.Contains(1, observed);
	}

	[Fact]
	public void Notify_WhenMultipleThreadsHaveDifferentContexts_RoutesPerDispatchTimeContext() {
		IntegrationTestServices.BuildWithStore(out var store, out var dispatcher);
		store.AddFeature<IntegrationTestState>(new IntegrationTestFeature());
		using var sub = store.Subscribe<IntegrationTestState>(_ => { });

		var ctxA = new RecordingSyncContext();
		var ctxB = new RecordingSyncContext();

		Parallel.Invoke(
			() => {
				SynchronizationContext.SetSynchronizationContext(ctxA);
				try {
					dispatcher.Dispatch(new IntegrationTestActions.Increment());
				} finally {
					SynchronizationContext.SetSynchronizationContext(null);
				}
			},
			() => {
				SynchronizationContext.SetSynchronizationContext(ctxB);
				try {
					dispatcher.Dispatch(new IntegrationTestActions.Increment());
				} finally {
					SynchronizationContext.SetSynchronizationContext(null);
				}
			}
		);

		Assert.NotEmpty(ctxA.Posts);
		Assert.NotEmpty(ctxB.Posts);
	}

	[Fact]
	public void Notify_WhenSyncContextChangesBetweenDispatches_ReadsFreshCurrentEachTime() {
		ClearSyncContext();
		IntegrationTestServices.BuildWithStore(out var store, out var dispatcher);
		store.AddFeature<IntegrationTestState>(new IntegrationTestFeature());
		using var sub = store.Subscribe<IntegrationTestState>(_ => { });

		var ctxBefore = new RecordingSyncContext();
		var ctxAfter = new RecordingSyncContext();

		SynchronizationContext.SetSynchronizationContext(ctxBefore);
		try {
			dispatcher.Dispatch(new IntegrationTestActions.Increment());
		} finally {
			SynchronizationContext.SetSynchronizationContext(null);
		}

		SynchronizationContext.SetSynchronizationContext(ctxAfter);
		try {
			dispatcher.Dispatch(new IntegrationTestActions.Increment());
		} finally {
			SynchronizationContext.SetSynchronizationContext(null);
		}

		Assert.NotEmpty(ctxBefore.Posts);
		Assert.NotEmpty(ctxAfter.Posts);
	}
}
