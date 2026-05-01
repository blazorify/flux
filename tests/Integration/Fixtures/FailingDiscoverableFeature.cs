using Blazorify.Flux.Core;

namespace Blazorify.Flux.Tests.Integration.Fixtures;

public class FailingDiscoverableFeature : Feature<FailingDiscoverableState> {
	public FailingDiscoverableFeature() {
		throw new InvalidOperationException("Discoverable feature constructor failure");
	}

	protected override void ConfigureReducers(ReducerBuilder builder) { }
	protected override void ConfigureEffects(EffectBuilder builder) { }
}
