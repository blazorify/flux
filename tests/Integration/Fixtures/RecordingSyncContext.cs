using System.Collections.Concurrent;

namespace Blazorify.Flux.Tests.Integration.Fixtures;

public sealed class RecordingSyncContext : SynchronizationContext {
	private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> posts = new();

	public IReadOnlyCollection<(SendOrPostCallback Callback, object? State)> Posts =>
		this.posts.ToArray();

	// Inline-invoke matches the Blazor RendererSynchronizationContext semantic that callbacks DO eventually run; lets test assertions read final state without needing to drain a queue.
	public override void Post(SendOrPostCallback d, object? state) {
		this.posts.Enqueue((d, state));
		d(state);
	}

	// NotifySubscribers contracts to async-dispatch (Post). A Send call is a regression.
	public override void Send(SendOrPostCallback d, object? state) {
		throw new InvalidOperationException(
			"Store.NotifySubscribers must Post (async dispatch), not Send (sync). " +
			"This call is unexpected; failing fast surfaces any regression.");
	}
}
