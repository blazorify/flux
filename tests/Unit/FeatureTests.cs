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

		// TestFeature registers 4 reducers (Increment, Decrement, IncrementBy, SetName).
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
		// TestFeature has no reducer for `Unregistered` — Feature.cs:49 returns early.
		var feature = new TestFeature();
		var received = new List<TestState>();

		feature.Reduce(new TestActions.Unregistered(), state => received.Add(state));

		Assert.Empty(received);
	}

	[Fact]
	public void Reduce_WhenStateUnchanged_DoesNotInvokeCallback() {
		// Default Name == string.Empty. SetName("") produces identical state — ApplyChanges
		// returns false (State.cs:23 ValueComparer.Equals), so callback is NOT invoked.
		var feature = new TestFeature();
		var received = new List<TestState>();

		feature.Reduce(new TestActions.SetName(string.Empty), state => received.Add(state));

		Assert.Empty(received);
	}

	[Fact]
	public void Reduce_WhenReducerProducesNewState_StatePropertyIsUpdated() {
		// Covers D-31 string property of TestState.
		var feature = new TestFeature();

		feature.Reduce(new TestActions.SetName("foo"), _ => { });

		Assert.Equal("foo", feature.State.Name);
	}

	[Fact]
	public void Reduce_WhenMultipleReducersForAction_AllRun() {
		// Two On<Increment> registrations both run. First adds 1, second adds 10.
		// Feature.cs:53 foreach reducer applies them sequentially; callback fires
		// after each ApplyChanges that detects a change.
		var feature = new MultiReducerFeature();
		var received = new List<TestState>();

		feature.Reduce(new TestActions.Increment(), state => received.Add(state));

		Assert.Equal(2, received.Count);          // both reducers ran and produced changes
		Assert.Equal(11, feature.State.Counter);  // 0 + 1 + 10 = 11
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
		// TestFeature has no effect for Unregistered — Feature.cs:63 returns early.
		var feature = new TestFeature();
		var mockDispatcher = new Mock<IDispatcher>();

		await feature.Effect(mockDispatcher.Object, new TestActions.Unregistered());
		// reaching here = no exception thrown
	}

	[Fact]
	public async Task Effect_WhenMatchingAction_InvokesEffect() {
		// TestFeature's Throw effect throws InvalidOperationException.
		// EffectBuilder.On<TAction>(Action<...>) wraps in Task.CompletedTask, so the throw
		// surfaces as a faulted Task — Assert.ThrowsAsync handles this cleanly.
		var feature = new TestFeature();
		var mockDispatcher = new Mock<IDispatcher>();

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => feature.Effect(mockDispatcher.Object, new TestActions.Throw())
		);
	}

	[Fact]
	public async Task Effect_WhenAsyncEffectDispatchesFollowUp_AwaitsCompletion() {
		// Verifies Feature.Effect awaits the effect lambda (Feature.cs:67-69 foreach + await).
		// AsyncEffectFeature dispatches Decrement from inside its effect after Task.Yield.
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
			// Async effect that dispatches a follow-up action.
			// Tests that Feature.Effect awaits the Task returned from the lambda.
			builder.On<TestActions.Increment>(async (dispatcher, _) => {
				await Task.Yield();   // forces async state machine; not fire-and-forget
				dispatcher.Dispatch(new TestActions.Decrement());
			});
		}
	}
}
