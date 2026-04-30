using Blazorify.Flux.Core;

namespace Blazorify.Flux.Tests.Unit.Fixtures;

public class FailingFeature : Feature<FailingState> {
	public FailingFeature() {
		throw new InvalidOperationException("Simulated constructor failure");
	}

	protected override void ConfigureReducers(ReducerBuilder builder) { }
	protected override void ConfigureEffects(EffectBuilder builder) { }
}
