using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blazorify.Flux.Core {
	public class Store : IStore {
		private readonly IOptions<BlazorifyFluxOptions> optionsAccessor;
		private readonly IServiceProvider serviceProvider;
		private readonly ILogger<Store> logger;

		private Boolean isInitialized = false;

		private readonly Dictionary<Type, Object> features = [];
		private readonly Dictionary<Type, Action<IAction, Action<Object?>>> reducers = [];
		private readonly Dictionary<Type, Action<IAction>> effects = [];

		private readonly ConcurrentQueue<IAction> queuedActions = [];
		private readonly Dictionary<Type, List<Delegate>> subscribers = [];

		public Store(
			IOptions<BlazorifyFluxOptions> optionsAccessor,
			IServiceProvider serviceProvider,
			ILogger<Store> logger
		) {
			this.optionsAccessor = optionsAccessor;
			this.serviceProvider = serviceProvider;
			this.logger = logger;
		}

		public void Initialize() {
			if (this.isInitialized) {
				return;
			}

			var featureTypes = this.optionsAccessor.Value.Assemblies
				.SelectMany(assembly => assembly.GetTypes())
				.Where(type => type.BaseType is { IsGenericType: true } && type.BaseType.GetGenericTypeDefinition() == typeof(FeatureBase<>));

			foreach (var featureType in featureTypes) {
				try {
					var feature = ActivatorUtilities.CreateInstance(this.serviceProvider, featureType);

					this.logger.LogDebug("Feature '{featureType}' has been discovered", featureType);

					this.AddFeature((dynamic)feature);

					this.logger.LogDebug("Feature '{featureType}' has been added", featureType);
				} catch (Exception ex) {
					this.logger.LogError(ex, ex.Message);
				}
			}

			this.isInitialized = true;
		}

		public void AddFeature<TState>(IFeature<TState> feature) where TState : class, new() {
			ArgumentNullException.ThrowIfNull(feature);

			if (!this.features.TryAdd(typeof(TState), feature)) {
				return;
			}

			this.reducers[typeof(TState)] = (action, callback) => {
				feature.Reduce(action, (state) => {
					this.NotifySubscribers(state);
				});
			};

			this.effects[typeof(TState)] = (action) => {
				feature.Effect(action);
			};
		}

		public IFeature<TState>? GetFeature<TState>() where TState : class, new() {
			if (this.features.TryGetValue(typeof(TState), out var feature)) {
				return (IFeature<TState>)feature;
			}

			return null;
		}

		public IDisposable Subscribe(Action callback) {
			return this.Subscribe<State>(state => { callback(); });
		}

		public IDisposable Subscribe<TState>(Action<TState> callback) where TState : class, new() {
			ArgumentNullException.ThrowIfNull(callback);

			var stateType = typeof(TState);

			lock (this.subscribers) {
				if (!this.subscribers.TryGetValue(stateType, out var subscribers)) {
					this.subscribers.Add(stateType, subscribers = []);
				}

				subscribers.Add(callback);
			}

			var subscription = new Subscription(() => {
				lock (this.subscribers) {
					this.subscribers[stateType].Remove(callback);
				}
			});

			var feature = this.GetFeature<TState>();

			if (feature is not null) {
				callback.Invoke(feature.State);
			}

			return subscription;
		}

		private void NotifySubscribers<TState>(TState state) where TState : class {
			var stateType = typeof(TState);

			lock (this.subscribers) {
				if (this.subscribers.TryGetValue(stateType, out var subscribers)) {
					foreach (var subscriber in subscribers.Cast<Action<TState>>()) {
						subscriber(state);
					}
				}
			}
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

		private void DispatchQueuedActions() {
			do {
				IAction? action;

				lock (this.queuedActions) {
					if (!this.queuedActions.TryDequeue(out action)) {
						return;
					}
				}

				foreach (var (stateType, reduce) in this.reducers) {
					reduce(action, (state) => {
						this.NotifySubscribers(state!);
					});
				}

				foreach (var (stateType, effect) in this.effects) {
					effect(action);
				}
			} while (true);
		}
	}
}