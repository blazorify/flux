using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Moq;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

public class FeatureTests {
	// === ConfigureReducers ===

	[Fact]
	public void ConfigureReducers_WhenReducerRegistered_IsInReducersCollection() {
		var feature = new TestFeature();

		Assert.NotEmpty(feature.Reducers);
	}

	// === Reduce ===

	[Fact]
	public void Reduce_WhenMatchingAction_InvokesCallbackWithNewState() {
		var feature = new TestFeature();
		var received = new List<TestState>();

		feature.Reduce(new TestActions.Increment(), state => received.Add(state));

		Assert.Single(received);
		Assert.Equal(1, received[0].Counter);
	}

	[Fact]
	public void Reduce_WhenNoMatchingAction_DoesNotInvokeCallback() {
		var feature = new TestFeature();
		var received = new List<TestState>();

		feature.Reduce(new TestActions.Unregistered(), state => received.Add(state));

		Assert.Empty(received);
	}

	[Fact]
	public void Reduce_WhenStateUnchanged_DoesNotInvokeCallback() {
		// SetName("") on a default state produces identical values; the callback
		// must NOT fire when ApplyChanges detects no change.
		var feature = new TestFeature();
		var received = new List<TestState>();

		feature.Reduce(new TestActions.SetName(string.Empty), state => received.Add(state));

		Assert.Empty(received);
	}

	[Fact]
	public void Reduce_WhenReducerProducesNewState_StatePropertyIsUpdated() {
		var feature = new TestFeature();

		feature.Reduce(new TestActions.SetName("foo"), _ => { });

		Assert.Equal("foo", feature.State.Name);
	}

	[Fact]
	public void Reduce_WhenMultipleReducersForAction_AllRun() {
		// Two On<Increment> registrations must both run sequentially, with the callback
		// firing after each one that produces a change. First adds 1, second adds 10.
		var feature = new MultiReducerFeature();
		var received = new List<TestState>();

		feature.Reduce(new TestActions.Increment(), state => received.Add(state));

		Assert.Equal(2, received.Count);
		Assert.Equal(11, feature.State.Counter);
	}

	// === Name ===

	[Fact]
	public void Name_ReturnsClassName() {
		var feature = new TestFeature();

		Assert.Equal("TestFeature", feature.Name);
	}

	// === Effect (async) ===

	[Fact]
	public async Task Effect_WhenNoMatchingAction_DoesNotThrow() {
		var feature = new TestFeature();
		var mockDispatcher = new Mock<IDispatcher>();

		await feature.Effect(mockDispatcher.Object, new TestActions.Unregistered());
	}

	[Fact]
	public async Task Effect_WhenMatchingAction_InvokesEffect() {
		// The synchronous `Action`-based effect overload wraps in Task.CompletedTask,
		// so a throw surfaces as a faulted Task and ThrowsAsync sees it.
		var feature = new TestFeature();
		var mockDispatcher = new Mock<IDispatcher>();

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => feature.Effect(mockDispatcher.Object, new TestActions.Throw())
		);
	}

	[Fact]
	public async Task Effect_WhenAsyncEffectDispatchesFollowUp_AwaitsCompletion() {
		// Effect must await the effect lambda — fire-and-forget would let the post-Yield
		// dispatch run after the test asserts and miss the Decrement.
		var feature = new AsyncEffectFeature();
		var mockDispatcher = new Mock<IDispatcher>();

		await feature.Effect(mockDispatcher.Object, new TestActions.Increment());

		mockDispatcher.Verify(
			d => d.Dispatch(It.IsAny<TestActions.Decrement>()),
			Times.Once
		);
	}

	// === Inline fixtures (private nested) ===

	private class MultiReducerFeature : Feature<TestState> {
		protected override void ConfigureReducers(ReducerBuilder builder) {
			builder.On<TestActions.Increment>((state, _) => state with {
				Counter = state.Counter + 1,
			});

			builder.On<TestActions.Increment>((state, _) => state with {
				Counter = state.Counter + 10,
			});
		}

		protected override void ConfigureEffects(EffectBuilder builder) { }
	}

	private class AsyncEffectFeature : Feature<TestState> {
		protected override void ConfigureReducers(ReducerBuilder builder) { }

		protected override void ConfigureEffects(EffectBuilder builder) {
			builder.On<TestActions.Increment>(async (dispatcher, _) => {
				await Task.Yield();   // forces a real async hop so the test catches missing await
				dispatcher.Dispatch(new TestActions.Decrement());
			});
		}
	}
}
