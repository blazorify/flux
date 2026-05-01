using Blazorify.Flux.Core;

namespace Blazorify.Flux.Tests.Components.Fixtures;

public class ComponentTestFeature : Feature<ComponentTestState> {
	protected override void ConfigureReducers(ReducerBuilder builder) {
		builder.On<ComponentTestActions.Increment>((state, _) => state with {
			Counter = state.Counter + 1,
		});

		builder.On<ComponentTestActions.IncrementBy>((state, action) => state with {
			Counter = state.Counter + action.Amount,
		});

		builder.On<ComponentTestActions.SetName>((state, action) => state with {
			Name = action.Value,
		});
	}

	protected override void ConfigureEffects(EffectBuilder builder) {
	}
}
