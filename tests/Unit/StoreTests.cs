using Blazorify.Flux.Core;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

public class StoreTests {
	// === Helpers ===

	// xUnit may set a SyncContext on the test thread; Store captures it on
	// construction and would Post notifications to a deferred queue, breaking
	// synchronous asserts. Clear it first so callbacks fire inline.
	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	private static Store BuildStoreWithoutFeature() {
		ClearSyncContext();
		return new Store(
			TestServices.Options(),
			TestServices.BuildEmpty(),
			NullLogger<Store>.Instance
		);
	}

	private static Store BuildStoreWithDispatcher(out Dispatcher dispatcher) {
		ClearSyncContext();
		TestServices.BuildWithStore(out var store, out dispatcher);
		return store;
	}

	// === AddFeature ===

	[Fact]
	public void AddFeature_WhenFeatureRegistered_IsRetrievable() {
		var store = BuildStoreWithoutFeature();
		var feature = new TestFeature();

		store.AddFeature<TestState>(feature);

		Assert.NotNull(store.GetFeature<TestState>());
		Assert.Same(feature, store.GetFeature<TestState>());
	}

	[Fact]
	public void AddFeature_WhenFeatureAlreadyRegistered_DoesNotReplace() {
		var store = BuildStoreWithoutFeature();
		var first = new TestFeature();
		var second = new TestFeature();

		store.AddFeature<TestState>(first);
		store.AddFeature<TestState>(second);

		Assert.Same(first, store.GetFeature<TestState>());
	}

	[Fact]
	public void AddFeature_WhenNullFeature_ThrowsArgumentNullException() {
		var store = BuildStoreWithoutFeature();

		Assert.Throws<ArgumentNullException>(() => store.AddFeature<TestState>(null!));
	}

	// === GetFeature ===

	[Fact]
	public void GetFeature_WhenStateNotRegistered_ReturnsNull() {
		var store = BuildStoreWithoutFeature();

		Assert.Null(store.GetFeature<TestState>());
	}

	// === Subscribe (replay contract) ===

	[Fact]
	public void Subscribe_WhenFeatureRegistered_FiresCallbackImmediately() {
		var store = BuildStoreWithoutFeature();
		store.AddFeature<TestState>(new TestFeature());
		var received = new List<TestState>();

		using var sub = store.Subscribe<TestState>(state => received.Add(state));

		Assert.Single(received);
		Assert.Equal(0, received[0].Counter);
		Assert.Equal(string.Empty, received[0].Name);
	}

	[Fact]
	public void Subscribe_WhenFeatureNotRegistered_DoesNotFireCallback() {
		var store = BuildStoreWithoutFeature();
		var received = new List<TestState>();

		using var sub = store.Subscribe<TestState>(state => received.Add(state));

		Assert.Empty(received);
	}

	[Fact]
	public void Subscribe_AfterDispatch_FiresCallbackWithNewState() {
		var store = BuildStoreWithDispatcher(out var dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		var received = new List<TestState>();
		using var sub = store.Subscribe<TestState>(state => received.Add(state));

		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(2, received.Count);
		Assert.Equal(0, received[0].Counter);   // initial replay
		Assert.Equal(1, received[1].Counter);   // after dispatch
	}

	[Fact]
	public void Subscribe_WhenActionCausesNoChange_DoesNotFireCallback() {
		var store = BuildStoreWithDispatcher(out var dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		var received = new List<TestState>();
		using var sub = store.Subscribe<TestState>(state => received.Add(state));

		dispatcher.Dispatch(new TestActions.Unregistered());

		Assert.Single(received);   // only the initial replay; no notification
	}

	[Fact]
	public void Subscribe_WhenDisposed_DoesNotFireOnSubsequentDispatch() {
		var store = BuildStoreWithDispatcher(out var dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		var received = new List<TestState>();
		var sub = store.Subscribe<TestState>(state => received.Add(state));
		var countAtSubscribe = received.Count;   // 1

		sub.Dispose();
		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(countAtSubscribe, received.Count);
	}

	[Fact]
	public void Subscribe_WhenNullCallback_ThrowsArgumentNullException() {
		var store = BuildStoreWithoutFeature();

		Assert.Throws<ArgumentNullException>(() => store.Subscribe<TestState>(null!));
	}

	[Fact]
	public void Subscribe_MultipleSubscribers_AllNotified() {
		var store = BuildStoreWithDispatcher(out var dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		var firstReceived = new List<TestState>();
		var secondReceived = new List<TestState>();

		using var sub1 = store.Subscribe<TestState>(state => firstReceived.Add(state));
		using var sub2 = store.Subscribe<TestState>(state => secondReceived.Add(state));
		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(2, firstReceived.Count);    // replay + dispatch
		Assert.Equal(2, secondReceived.Count);   // replay + dispatch
		Assert.Equal(1, firstReceived[1].Counter);
		Assert.Equal(1, secondReceived[1].Counter);
	}

	// === Initialize ===

	[Fact]
	public void Initialize_WhenAssemblyContainsFeature_RegistersFeature() {
		var sp = TestServices.BuildEmpty();
		var store = new Store(
			TestServices.Options(typeof(TestFeature).Assembly),
			sp,
			NullLogger<Store>.Instance
		);

		store.Initialize();

		Assert.NotNull(store.GetFeature<TestState>());
	}

	[Fact]
	public void Initialize_WhenFeatureFails_ContinuesToNextFeature() {
		// One feature's constructor throwing must not prevent sibling features
		// in the same assembly from registering.
		var sp = TestServices.BuildEmpty();
		var store = new Store(
			TestServices.Options(typeof(TestFeature).Assembly),
			sp,
			NullLogger<Store>.Instance
		);

		store.Initialize();

		Assert.Null(store.GetFeature<FailingState>());     // failing feature did NOT register
		Assert.NotNull(store.GetFeature<TestState>());     // healthy feature DID register
	}

	[Fact]
	public void Initialize_WhenCalledTwice_IsIdempotent() {
		var sp = TestServices.BuildEmpty();
		var store = new Store(
			TestServices.Options(typeof(TestFeature).Assembly),
			sp,
			NullLogger<Store>.Instance
		);
		store.Initialize();
		var featureAfterFirst = store.GetFeature<TestState>();

		store.Initialize();   // must be a no-op

		Assert.Same(featureAfterFirst, store.GetFeature<TestState>());
	}

	[Fact]
	public void Initialize_WhenAssemblyEmpty_NoFeatureRegistered() {
		var sp = TestServices.BuildEmpty();
		var store = new Store(
			TestServices.Options(),   // no assemblies
			sp,
			NullLogger<Store>.Instance
		);

		store.Initialize();

		Assert.Null(store.GetFeature<TestState>());
		Assert.Null(store.GetFeature<FailingState>());
	}

	// === ProcessAction (multi-feature dispatch) ===

	[Fact]
	public void ProcessAction_WhenMultipleFeatures_AllReducersRun() {
		// Dispatch must run reducers across ALL registered features, not just
		// the one whose state type matches the action.
		var store = BuildStoreWithDispatcher(out var dispatcher);
		store.AddFeature<TestState>(new TestFeature());
		store.AddFeature<SecondaryState>(new SecondaryFeature());
		var primaryReceived = new List<TestState>();
		var secondaryReceived = new List<SecondaryState>();
		using var s1 = store.Subscribe<TestState>(state => primaryReceived.Add(state));
		using var s2 = store.Subscribe<SecondaryState>(state => secondaryReceived.Add(state));

		dispatcher.Dispatch(new TestActions.Increment());

		// primary: replay (Counter=0) + dispatch (Counter=1) = 2 entries
		Assert.Equal(2, primaryReceived.Count);
		Assert.Equal(1, primaryReceived[1].Counter);
		// secondary: replay (Hits=0) + dispatch (Hits=1) = 2 entries — its reducer also matched Increment
		Assert.Equal(2, secondaryReceived.Count);
		Assert.Equal(1, secondaryReceived[1].Hits);
	}

	// === Dispose-time subscriber pruning ===

	[Fact]
	public void Subscribe_AndDispose_PrunesSubscribersDictionary() {
		// Subscription disposal removes the callback and also drops the dictionary
		// entry when the per-state list becomes empty (no leak across resubscribes).
		var store = BuildStoreWithoutFeature();
		store.AddFeature<TestState>(new TestFeature());

		var sub = store.Subscribe<TestState>(_ => { });
		Assert.True(store.HasSubscriberEntry(typeof(TestState)));

		sub.Dispose();

		Assert.False(store.HasSubscriberEntry(typeof(TestState)));
	}

	// === Inline fixtures for multi-feature scenario only ===

	private record SecondaryState {
		public int Hits { get; init; }
	}

	private class SecondaryFeature : Feature<SecondaryState> {
		protected override void ConfigureReducers(ReducerBuilder builder) {
			builder.On<TestActions.Increment>((state, _) => state with {
				Hits = state.Hits + 1,
			});
		}

		protected override void ConfigureEffects(EffectBuilder builder) { }
	}
}
