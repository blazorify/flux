namespace Blazorify.Flux.Tests.Unit.Fixtures;

public record TestState {
	public int Counter { get; init; }
	public string Name { get; init; } = string.Empty;
}
