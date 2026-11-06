using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Blazorify.Flux.Tests.Unit.Fixtures;

/// <summary>
/// Keeps every entry logged at Warning or above, so tests can assert that a failure was reported.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T> {
	private readonly ConcurrentQueue<(LogLevel Level, Exception? Exception, string Message)> entries = new();

	public IReadOnlyCollection<(LogLevel Level, Exception? Exception, string Message)> Entries =>
		this.entries.ToArray();

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter
	) {
		if (logLevel >= LogLevel.Warning) {
			this.entries.Enqueue((logLevel, exception, formatter(state, exception)));
		}
	}
}
