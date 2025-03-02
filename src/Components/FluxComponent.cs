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

		protected void Dispatch<TAction>() where TAction : IAction, new() {
			this.logger.LogDebug("[{actionType}] Dispatching action", typeof(TAction));

			this.store.Dispatch<TAction>();

			this.logger.LogDebug("[{actionType}] Action has been dispatched", typeof(TAction));
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
