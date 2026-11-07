using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Tests.Unit.Fixtures;

public static class EffectFailureActions {
	public record FailSync : IAction;
	public record FailAsync : IAction;
	public record Succeeded : IAction;
}

/// <summary>
/// Effects that throw before and after their first await. The reducers count successful follow-ups
/// so a test can see that effects keep working after one has failed.
/// </summary>
public class EffectFailureFeature : Feature<EffectFailureState> {
	protected override void ConfigureReducers(ReducerBuilder builder) {
		builder.On<EffectFailureActions.Succeeded>((state, _) => state with {
			Runs = state.Runs + 1,
		});
	}

	protected override void ConfigureEffects(EffectBuilder builder) {
		builder.On<EffectFailureActions.FailSync>((dispatcher, _) => {
			throw new InvalidOperationException("Effect failed before awaiting");
		});

		builder.On<EffectFailureActions.FailAsync>(async (dispatcher, _) => {
			await Task.Yield();
			throw new InvalidOperationException("Effect failed after awaiting");
		});
	}
}
