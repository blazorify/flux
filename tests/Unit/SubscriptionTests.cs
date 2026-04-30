using Blazorify.Flux.Core;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

public class SubscriptionTests {
	// Clear SyncContext so dispatch fires reducers synchronously (see StoreTests for rationale).
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
		// Subscription.cs:13-16 — `disposed` bool flag short-circuits the second call.
		var unsubscribeCount = 0;
		var sub = new Subscription(() => unsubscribeCount++);

		sub.Dispose();
		sub.Dispose();

		Assert.Equal(1, unsubscribeCount);
	}

	// === Subscription + Store integration ===

	[Fact]
	public void Dispose_WhenCalled_RemovesSubscriberFromStore() {
		// Subscribe registers a delegate; Dispose runs the unsubscribe Action which
		// removes the delegate from Store.subscribers (Store.cs:116-120). Subsequent
		// dispatches must NOT invoke the original callback.
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
