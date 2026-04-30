using Blazorify.Flux.Core;

namespace Blazorify.Flux.Tests.Unit.Fixtures;

public class TestFeature : Feature<TestState> {
	protected override void ConfigureReducers(ReducerBuilder builder) {
		builder.On<TestActions.Increment>((state, _) => state with {
			Counter = state.Counter + 1,
		});

		builder.On<TestActions.Decrement>((state, _) => state with {
			Counter = state.Counter - 1,
		});

		builder.On<TestActions.IncrementBy>((state, action) => state with {
			Counter = state.Counter + action.Amount,
		});

		builder.On<TestActions.SetName>((state, action) => state with {
			Name = action.Value,
		});
	}

	protected override void ConfigureEffects(EffectBuilder builder) {
		builder.On<TestActions.Throw>((dispatcher, _) => {
			throw new InvalidOperationException("Test effect failure");
		});
	}
}
