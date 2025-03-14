using System;
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
				.Where(type => type.BaseType is { IsGenericType: true } && type.BaseType.GetGenericTypeDefinition() == typeof(Feature<>));

			foreach (var featureType in featureTypes) {
				try {
					var feature = ActivatorUtilities.CreateInstance(this.serviceProvider, featureType);

					this.AddFeature((dynamic)feature);
				} catch (Exception ex) {
					this.logger.LogError(ex, "Failed to initialize feature '{featureType}'", featureType);
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

			this.effects[typeof(TState)] = async (action) => {
				var dispatcher = this.serviceProvider.GetRequiredService<IDispatcher>();

				await feature.Effect(dispatcher, action);
			};
		}

		public IFeature<TState>? GetFeature<TState>() where TState : class, new() {
			if (this.features.TryGetValue(typeof(TState), out var feature)) {
				return (IFeature<TState>)feature;
			}

			return null;
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

		void IStore.ProcessAction(IAction action) {
			foreach (var (stateType, reduce) in this.reducers) {
				reduce(action, (state) => {
					this.NotifySubscribers(state!);
				});
			}

			foreach (var (stateType, effect) in this.effects) {
				effect(action);
			}
		}
	}
}