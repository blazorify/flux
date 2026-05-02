using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Tests.Unit.Fixtures;

public class SecondaryFeature : Feature<SecondaryState> {
	protected override void ConfigureReducers(ReducerBuilder builder) {
		builder.On<SecondaryActions.SetLabel>((state, action) => state with {
			Label = action.Label,
		});

		builder.On<SecondaryActions.ToggleFlag>((state, _) => state with {
			Flag = !state.Flag,
		});
	}

	protected override void ConfigureEffects(EffectBuilder builder) { }
}

public static class SecondaryActions {
	public record SetLabel(string Label) : IAction;
	public record ToggleFlag : IAction;
}
