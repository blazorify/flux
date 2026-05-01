using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Tests.Integration.Fixtures;

public static class IntegrationTestActions {
	public record Increment : IAction;
	public record IncrementBy(int Amount = 0) : IAction;
	public record SetName(string Value) : IAction;
	public record StartAsyncWork : IAction;
	public record AsyncWorkCompleted(int Result) : IAction;
}
