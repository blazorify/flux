using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Tests.Unit.Fixtures;

public static class TestActions {
	public record Increment : IAction;
	public record Decrement : IAction;
	public record IncrementBy(int Amount = 0) : IAction;
	public record SetName(string Value) : IAction;
	public record Throw : IAction;
	public record Unregistered : IAction;
}
