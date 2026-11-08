using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Options;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

/// <summary>
/// The notify path under the conditions a Blazor Server app produces: subscribers that subscribe or
/// unsubscribe while being notified, subscribers that throw, notifications that run late or out of order,
/// and effects that fail.
/// </summary>
public class NotificationTests {
	// === Helpers ===

	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	private static Store Build(out Dispatcher dispatcher, out RecordingLogger<Store> logger) {
		ClearSyncContext();

		var recording = new RecordingLogger<Store>();
		var services = new ServiceCollection();

		services.AddSingleton<IStore>(sp => new Store(
			Microsoft.Extensions.Options.Options.Create(new BlazorifyFluxOptions()),
			sp,
			recording
		));
		services.AddSingleton<IDispatcher>(sp => new Dispatcher(
			sp.GetRequiredService<IStore>(),
			NullLogger<Dispatcher>.Instance
		));

		var provider = services.BuildServiceProvider();
		var store = (Store)provider.GetRequiredService<IStore>();
		dispatcher = (Dispatcher)provider.GetRequiredService<IDispatcher>();
		logger = recording;

		store.AddFeature<TestState>(new TestFeature());
		store.AddFeature<EffectFailureState>(new EffectFailureFeature());

		return store;
	}

	private static async Task WaitUntil(Func<bool> condition) {
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (!condition() && DateTime.UtcNow < deadline) {
			await Task.Delay(10);
		}
	}

	// === Subscribers that change the subscriber list while being notified ===

	[Fact]
	public void Notify_WhenSubscriberSubscribesDuringNotification_LaterSubscribersStillNotified() {
		var store = Build(out var dispatcher, out var logger);
		var added = new List<IDisposable>();
		var later = new List<int>();

		// A render that mounts a child component which subscribes too, on the notifying thread.
		using var first = store.Subscribe<TestState>(state => {
			if (state.Counter == 1) {
				added.Add(store.Subscribe<TestState>(_ => { }));
			}
		});
		using var second = store.Subscribe<TestState>(state => later.Add(state.Counter));

		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(new[] { 0, 1 }, later);
		Assert.Single(added);
		Assert.Empty(logger.Entries);
	}

	[Fact]
	public void Notify_WhenSubscriberUnsubscribesDuringNotification_LaterSubscribersStillNotified() {
		var store = Build(out var dispatcher, out var logger);
		var later = new List<int>();

		IDisposable? self = null;
		self = store.Subscribe<TestState>(state => {
			if (state.Counter == 1) {
				self!.Dispose();
			}
		});
		using var second = store.Subscribe<TestState>(state => later.Add(state.Counter));

		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(new[] { 0, 1 }, later);
		Assert.Equal(1, store.SubscriberCount(typeof(TestState)));
		Assert.Empty(logger.Entries);
	}

	[Fact]
	public void Notify_WhenSubscriberDisposesALaterSubscriber_DisposedSubscriberIsNotCalled() {
		var store = Build(out var dispatcher, out _);
		var removedCalls = new List<int>();

		IDisposable? removed = null;
		using var first = store.Subscribe<TestState>(state => {
			if (state.Counter == 1) {
				removed!.Dispose();
			}
		});
		removed = store.Subscribe<TestState>(state => removedCalls.Add(state.Counter));

		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(new[] { 0 }, removedCalls);
	}

	// === Subscribers that throw ===

	[Fact]
	public void Notify_WhenSubscriberThrows_OtherSubscribersAndEffectsStillRunAndFailureIsLogged() {
		var store = Build(out var dispatcher, out var logger);
		var later = new List<int>();

		using var throwing = store.Subscribe<TestState>(state => {
			if (state.Counter > 0) {
				throw new InvalidOperationException("subscriber failure");
			}
		});
		using var second = store.Subscribe<TestState>(state => later.Add(state.Counter));

		dispatcher.Dispatch(new TestActions.Increment());

		Assert.Equal(new[] { 0, 1 }, later);
		var entry = Assert.Single(logger.Entries);
		Assert.IsType<InvalidOperationException>(entry.Exception);
	}

	// === Notifications that run late, or out of order ===

	[Fact]
	public void Notify_WhenPostedNotificationsRunOutOfOrder_SubscriberEndsOnLatestState() {
		var store = Build(out var dispatcher, out _);
		var observed = new List<int>();
		using var sub = store.Subscribe<TestState>(state => observed.Add(state.Counter));

		var context = new QueueingSyncContext();
		SynchronizationContext.SetSynchronizationContext(context);
		try {
			dispatcher.Dispatch(new TestActions.Increment());
			dispatcher.Dispatch(new TestActions.Increment());
		} finally {
			SynchronizationContext.SetSynchronizationContext(null);
		}

		Assert.Equal(2, context.Count);

		// The second change is announced first. The subscriber must not be left on the first.
		context.RunAll(reverse: true);

		Assert.Equal(2, observed[^1]);
	}

	[Fact]
	public void Notify_WhenSubscriptionDisposedBeforePostedNotificationRuns_CallbackIsNotInvoked() {
		var store = Build(out var dispatcher, out _);
		var observed = new List<int>();
		var sub = store.Subscribe<TestState>(state => observed.Add(state.Counter));

		var context = new QueueingSyncContext();
		SynchronizationContext.SetSynchronizationContext(context);
		try {
			dispatcher.Dispatch(new TestActions.Increment());
		} finally {
			SynchronizationContext.SetSynchronizationContext(null);
		}

		// A component torn down between the dispatch and the renderer running the notification.
		sub.Dispose();
		context.RunAll();

		Assert.Equal(new[] { 0 }, observed);
	}

	[Fact]
	public void Notify_WhenManyThreadsDispatchConcurrently_SubscriberEndsOnFinalState() {
		var store = Build(out var dispatcher, out _);
		var last = -1;
		using var sub = store.Subscribe<TestState>(state => Volatile.Write(ref last, state.Counter));

		Parallel.For(0, 2000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, _ => {
			SynchronizationContext.SetSynchronizationContext(null);
			dispatcher.Dispatch(new TestActions.Increment());
		});

		Assert.Equal(2000, store.GetFeature<TestState>()!.State.Counter);
		Assert.Equal(2000, Volatile.Read(ref last));
	}

	// === Subscribe replay ===

	[Fact]
	public void Subscribe_WhenReplayThrows_SubscriptionIsRemovedAndExceptionPropagates() {
		var store = Build(out _, out _);

		Assert.Throws<InvalidOperationException>(() =>
			store.Subscribe<TestState>(_ => throw new InvalidOperationException("replay failure")));

		Assert.False(store.HasSubscriberEntry(typeof(TestState)));
	}

	// === Effects that fail ===

	[Fact]
	public async Task Effect_WhenItThrowsBeforeAwaiting_FailureIsLoggedAndDispatchDoesNotThrow() {
		var store = Build(out var dispatcher, out var logger);

		dispatcher.Dispatch(new EffectFailureActions.FailSync());
		await WaitUntil(() => logger.Entries.Count > 0);

		var entry = Assert.Single(logger.Entries);
		Assert.Equal("Effect failed before awaiting", entry.Exception?.Message);
		Assert.Contains(nameof(EffectFailureState), entry.Message);
	}

	[Fact]
	public async Task Effect_WhenItThrowsAfterAwaiting_FailureIsLogged() {
		var store = Build(out var dispatcher, out var logger);

		dispatcher.Dispatch(new EffectFailureActions.FailAsync());
		await WaitUntil(() => logger.Entries.Count > 0);

		var entry = Assert.Single(logger.Entries);
		Assert.Equal("Effect failed after awaiting", entry.Exception?.Message);
	}

	[Fact]
	public async Task Effect_AfterAnEffectFailed_LaterActionsStillProcessed() {
		var store = Build(out var dispatcher, out var logger);

		dispatcher.Dispatch(new EffectFailureActions.FailAsync());
		dispatcher.Dispatch(new EffectFailureActions.Succeeded());
		await WaitUntil(() => logger.Entries.Count > 0);

		Assert.Equal(1, store.GetFeature<EffectFailureState>()!.State.Runs);
	}

	[Fact]
	public void Effect_WhenDispatcherCannotBeResolved_FailureIsLoggedNotThrown() {
		ClearSyncContext();
		var logger = new RecordingLogger<Store>();
		var store = new Store(TestServices.Options(), TestServices.BuildEmpty(), logger);
		store.AddFeature<TestState>(new TestFeature());

		((IStore)store).ProcessAction(new TestActions.Increment());

		Assert.Equal(1, store.GetFeature<TestState>()!.State.Counter);
		var entry = Assert.Single(logger.Entries);
		Assert.IsType<InvalidOperationException>(entry.Exception);
	}
}
