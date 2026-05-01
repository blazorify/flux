using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Tests.Components.Fixtures;

public static class ComponentTestActions {
	public record Increment : IAction;
	public record IncrementBy(int Amount = 0) : IAction;
	public record SetName(string Value) : IAction;
}
