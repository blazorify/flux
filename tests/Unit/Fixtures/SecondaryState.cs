namespace Blazorify.Flux.Tests.Unit.Fixtures;

public record SecondaryState {
	public string Label { get; init; } = string.Empty;
	public bool Flag { get; init; } = false;
}
