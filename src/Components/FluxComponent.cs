using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Blazorify.Flux.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazorify.Flux.Components {
	public partial class FluxComponent : ComponentBase, IAsyncDisposable {
		private readonly List<IDisposable> disposables = [];

		[Inject]
		private IStore store { get; set; } = default!;

		[Inject]
		private IDispatcher dispatcher { get; set; } = default!;

		[Inject]
		private ILogger<FluxComponent> logger { get; set; } = default!;

		protected void Subscribe<TState>(Action<TState> callback) where TState : class, new() {
			this.logger.LogDebug("[{stateType}] Subscribing to state changes", typeof(TState));

			var disposable = this.store.Subscribe<TState>(async state => {
				this.logger.LogDebug("[{stateType}] State change received", typeof(TState));

				callback.Invoke(state);
				this.logger.LogDebug("[{stateType}] Callback for state change called", typeof(TState));

				await this.InvokeAsync(this.StateHasChanged);
				this.logger.LogDebug("[{stateType}] Component notified that state has been changed", typeof(TState));
			});

			this.logger.LogDebug("[{stateType}] Subscribed to state changes", typeof(TState));

			this.disposables.Add(disposable);

			this.logger.LogDebug("[{stateType}] IDisposable registered for subscription", typeof(TState));
		}

		protected void Subscribe<TState>(Func<TState, Task> callback) where TState : class, new() {
			ArgumentNullException.ThrowIfNull(callback);
			this.logger.LogDebug("[{stateType}] Subscribing to state changes (async)", typeof(TState));

			var disposable = this.store.Subscribe<TState>(async state => {
				this.logger.LogDebug("[{stateType}] State change received (async)", typeof(TState));

				// Caught here so a thrown user Task does not become an unobserved-task exception
				// on the renderer's SynchronizationContext (the inner lambda is async void at the
				// Action<TState> boundary).
				try {
					await callback(state);
				} catch (Exception ex) {
					this.logger.LogError(ex, "[{stateType}] User callback threw", typeof(TState));
				}
				this.logger.LogDebug("[{stateType}] Async callback for state change awaited", typeof(TState));

				// Intentionally OUTSIDE the catch — a renderer-marshalling failure is a real
				// component-lifecycle problem and must propagate.
				await this.InvokeAsync(this.StateHasChanged);
				this.logger.LogDebug("[{stateType}] Component notified that state has been changed", typeof(TState));
			});

			this.logger.LogDebug("[{stateType}] Subscribed to state changes (async)", typeof(TState));
			this.disposables.Add(disposable);
			this.logger.LogDebug("[{stateType}] IDisposable registered for subscription (async)", typeof(TState));
		}

		protected void Dispatch<TAction>() where TAction : IAction, new() {
			this.Dispatch(new TAction());
		}

		protected void Dispatch<TAction>(TAction action) where TAction : IAction {
			this.Dispatch((IAction)action);
		}

		protected void Dispatch<TAction>(Func<TAction> action) where TAction : IAction {
			this.Dispatch(action.Invoke());
		}

		protected void Dispatch<TAction>(Func<TAction, TAction> action) where TAction : IAction, new() {
			this.Dispatch(action.Invoke(new TAction()));
		}

		protected void Dispatch(IAction action) {
			this.logger.LogDebug("[{actionType}] Dispatching action", typeof(IAction));

			this.dispatcher.Dispatch(action);

			this.logger.LogDebug("[{actionType}] Action has been dispatched", typeof(IAction));
		}

		public async ValueTask DisposeAsync() {
			await Task.CompletedTask;

			this.logger.LogDebug("Disposing {count} subscriptions", this.disposables.Count);

			foreach (var disposable in this.disposables) {
				disposable.Dispose();
			}

			this.logger.LogDebug("Disposed {count} subscriptions", this.disposables.Count);

			this.disposables.Clear();

			this.logger.LogDebug("Disposables list has been cleared");
		}
	}
}
