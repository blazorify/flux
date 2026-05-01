using Blazorify.Flux.Core;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

public class SubscriptionTests {
	// xUnit's SyncContext would make Store post notifications to a deferred queue;
	// clear it so callbacks fire inline and synchronous asserts can see them.
	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	// === Subscription idempotency (in isolation) ===

	[Fact]
	public void Dispose_WhenCalledTwice_DoesNotThrow() {
		var sub = new Subscription(() => { });

		sub.Dispose();
		sub.Dispose();
		// reaching here = no exception
	}

	[Fact]
	public void Dispose_WhenCalledTwice_UnsubscribesOnlyOnce() {
		// Dispose must be idempotent — a second call must not run the unsubscribe action again.
		var unsubscribeCount = 0;
		var sub = new Subscription(() => unsubscribeCount++);

		sub.Dispose();
		sub.Dispose();

		Assert.Equal(1, unsubscribeCount);
	}

	// === Subscription + Store integration ===

	[Fact]
	public void Dispose_WhenCalled_RemovesSubscriberFromStore() {
		// Disposing the subscription must remove the callback from the Store so
		// subsequent dispatches do not invoke it.
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out var dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		var received = new List<TestState>();
		var sub = store.Subscribe<TestState>(state => received.Add(state));
		var countAfterSubscribe = received.Count;   // 1 (initial replay)

		sub.Dispose();
		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(countAfterSubscribe, received.Count);
	}

	[Fact]
	public void Dispose_WhenNotCalled_CallbackStillFiresAfterDispatch() {
		// Positive control — without Dispose, the subscriber receives the notification.
		// Confirms the test infrastructure isn't accidentally suppressing callbacks.
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out var dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		var received = new List<TestState>();
		using var sub = store.Subscribe<TestState>(state => received.Add(state));

		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(2, received.Count);   // replay + dispatch
		Assert.Equal(1, received[1].Counter);
	}
}
