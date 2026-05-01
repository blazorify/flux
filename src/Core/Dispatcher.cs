using System;
using System.Collections.Concurrent;
using Blazorify.Flux.Interfaces;
using Microsoft.Extensions.Logging;

namespace Blazorify.Flux.Core {
	public class Dispatcher : IDispatcher {
		private readonly IStore store;
		private readonly ILogger<Dispatcher> logger;

		private readonly ConcurrentQueue<IAction> queuedActions = [];

		public Dispatcher(
			IStore store,
			ILogger<Dispatcher> logger
		) {
			this.store = store;
			this.logger = logger;
		}

		private void DispatchQueuedActions() {
			do {
				IAction? action;

				lock (this.queuedActions) {
					if (!this.queuedActions.TryDequeue(out action)) {
						return;
					}
				}

				try {
					this.store.ProcessAction(action);
				} catch (Exception ex) {
					this.logger.LogError(ex, "Failed to process action {actionType}", action.GetType());
				}
			} while (true);
		}

		public void Dispatch<TAction>() where TAction : IAction, new() {
			this.Dispatch(new TAction());
		}

		public void Dispatch<TAction>(TAction action) where TAction : IAction {
			this.Dispatch((IAction)action);
		}

		public void Dispatch<TAction>(Func<TAction> action) where TAction : IAction {
			this.Dispatch(action.Invoke());
		}

		public void Dispatch<TAction>(Func<TAction, TAction> action) where TAction : IAction, new() {
			this.Dispatch(action.Invoke(new TAction()));
		}

		public void Dispatch(IAction action) {
			ArgumentNullException.ThrowIfNull(action);

			lock (this.queuedActions) {
				this.queuedActions.Enqueue(action);
			}

			this.DispatchQueuedActions();
		}
	}
}
