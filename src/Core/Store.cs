using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
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

			this.logger.LogDebug("Discovered {count} features: {features}", featureTypes.Count(), String.Join(";", featureTypes.Select(m => m.Name)));

			// Reflect once outside the loop, close + invoke per feature
			var addFeatureOpenGeneric = typeof(Store)
				.GetMethod(nameof(this.AddFeature), BindingFlags.Instance | BindingFlags.Public)!;

			foreach (var featureType in featureTypes) {
				try {
					this.logger.LogDebug("Instantiating feature: {featureType}", featureType);
					var feature = ActivatorUtilities.CreateInstance(this.serviceProvider, featureType);

					var stateType = featureType.BaseType!.GetGenericArguments()[0];
					var addFeatureClosedGeneric = addFeatureOpenGeneric.MakeGenericMethod(stateType);

					this.logger.LogDebug("Registering feature: {featureType}", featureType);
					addFeatureClosedGeneric.Invoke(this, [feature]);
					this.logger.LogDebug("Registered feature: {featureType}", featureType);
				} catch (Exception ex) {
					// Catches throws from CreateInstance / MakeGenericMethod AND TargetInvocationException from Invoke.
					this.logger.LogError(ex, "Failed to initialize feature '{featureType}'", featureType);
				}
			}

			this.isInitialized = true;
		}

		public void AddFeature<TState>(IFeature<TState> feature) where TState : class, new() {
			ArgumentNullException.ThrowIfNull(feature);

			this.logger.LogDebug("Add feature: {featureType}", typeof(TState));
			if (!this.features.TryAdd(typeof(TState), feature)) {
				this.logger.LogDebug("Failed to add feature: {featureType}", typeof(TState));
				return;
			}

			this.logger.LogDebug("Registering {featureType} feature reducers", typeof(TState));
			this.reducers[typeof(TState)] = (action, callback) => {
				feature.Reduce(action, (state) => {
					this.NotifySubscribers(state);
				});
			};

			this.logger.LogDebug("Registering {featureType} feature effects", typeof(TState));
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

			// Dispose-time pruning of empty subscriber lists
			var subscription = new Subscription(() => {
				lock (this.subscribers) {
					if (this.subscribers.TryGetValue(stateType, out var list)) {
						list.Remove(callback);
						if (list.Count == 0) {
							this.subscribers.Remove(stateType);
						}
					}
				}
			});

			var feature = this.GetFeature<TState>();

			if (feature is not null) {
				callback.Invoke(feature.State);
			}

			return subscription;
		}

		public ISelector<TState, TResult> Select<TState, TResult>(Func<TState, TResult> projector)
			where TState : class, new() {
			return this.Select(projector, ReferenceEqualityComparer.Instance, EqualityComparer<TResult>.Default);
		}

		public ISelector<TState, TResult> Select<TState, TResult>(
			Func<TState, TResult> projector,
			IEqualityComparer<TState> inputComparer
		) where TState : class, new() {
			return this.Select(projector, inputComparer, EqualityComparer<TResult>.Default);
		}

		public ISelector<TState, TResult> Select<TState, TResult>(
			Func<TState, TResult> projector,
			IEqualityComparer<TState> inputComparer,
			IEqualityComparer<TResult> outputComparer
		) where TState : class, new() {
			ArgumentNullException.ThrowIfNull(projector);
			ArgumentNullException.ThrowIfNull(inputComparer);
			ArgumentNullException.ThrowIfNull(outputComparer);

			return new MemoizedSelector<TState, TResult>(projector, inputComparer, outputComparer);
		}

		private void NotifySubscribers<TState>(TState state) where TState : class {
			var stateType = typeof(TState);

			// Read the ambient context once per call, before the lock — it's a thread-local read.
			var current = SynchronizationContext.Current;

			lock (this.subscribers) {
				if (this.subscribers.TryGetValue(stateType, out var subscribers)) {
					foreach (var @delegate in subscribers) {
						var subscriber = (Action<TState>)@delegate;

						if (current != null) {
							this.logger.LogDebug("Notifying subscriber for '{StateType}' via SynchronizationContext.", stateType.FullName);
							current.Post(_ => subscriber(state), null);
						} else {
							this.logger.LogDebug("Notifying subscriber for '{StateType}' directly (no SynchronizationContext).", stateType.FullName);
							subscriber(state);
						}
					}
				} else {
					this.logger.LogDebug("No subscribers found for '{StateType}'.", stateType.FullName);
				}
			}
		}

		internal Int32 SubscriberCount(Type stateType) {
			lock (this.subscribers) {
				return this.subscribers.TryGetValue(stateType, out var list) ? list.Count : 0;
			}
		}

		internal Boolean HasSubscriberEntry(Type stateType) {
			lock (this.subscribers) {
				return this.subscribers.ContainsKey(stateType);
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
