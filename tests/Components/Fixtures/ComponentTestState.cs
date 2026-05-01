namespace Blazorify.Flux.Tests.Components.Fixtures;

public record ComponentTestState {
	public int Counter { get; init; }
	public string Name { get; init; } = string.Empty;
}
