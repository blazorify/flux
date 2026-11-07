using System.Collections.Concurrent;

namespace Blazorify.Flux.Tests.Unit.Fixtures;

/// <summary>
/// Holds every Post until the test runs it, in whatever order the test chooses — the way a renderer
/// that is busy, or two renderers, can run notifications later and out of order.
/// </summary>
public sealed class QueueingSyncContext : SynchronizationContext {
	private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> posts = new();

	public int Count => this.posts.Count;

	public override void Post(SendOrPostCallback d, object? state) {
		this.posts.Enqueue((d, state));
	}

	public override void Send(SendOrPostCallback d, object? state) {
		throw new InvalidOperationException("Store must Post, not Send.");
	}

	public void RunAll(bool reverse = false) {
		var pending = new List<(SendOrPostCallback Callback, object? State)>();
		while (this.posts.TryDequeue(out var post)) {
			pending.Add(post);
		}

		if (reverse) {
			pending.Reverse();
		}

		foreach (var (callback, state) in pending) {
			callback(state);
		}
	}
}
