namespace Blazorify.Flux.Tests.Integration.Fixtures;

public record IntegrationTestState {
	public int Counter { get; init; }
	public string Name { get; init; } = string.Empty;
	public string LastEffectMarker { get; init; } = string.Empty;
}
