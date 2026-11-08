using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
		private readonly Dictionary<Type, Action<IAction>> reducers = [];
		private readonly Dictionary<Type, Func<IAction, Task>> effects = [];

		private readonly Dictionary<Type, List<SubscriberEntry>> subscribers = [];

		private readonly ConcurrentDictionary<ParameterizedSelectorKey, Object> parameterizedCache = new();

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
			// Subscribers are handed the feature's state as it is when they are called, not the snapshot
			// this reduce produced: two dispatches racing on different threads could otherwise announce
			// their snapshots in the opposite order and leave a subscriber holding the older one.
			this.reducers[typeof(TState)] = (action) => {
				feature.Reduce(action, _ => {
					this.NotifySubscribers(feature);
				});
			};

			this.logger.LogDebug("Registering {featureType} feature effects", typeof(TState));
			this.effects[typeof(TState)] = (action) => {
				var dispatcher = this.serviceProvider.GetRequiredService<IDispatcher>();

				return feature.Effect(dispatcher, action);
			};
		}

		public IFeature<TState>? GetFeature<TState>() where TState : class, new() {
			if (this.features.TryGetValue(typeof(TState), out var feature)) {
				return (IFeature<TState>)feature;
			}

			return null;
		}

		/// <summary>
		/// Registers <paramref name="wrapper"/> for <typeparamref name="TState"/> and, when the feature is
		/// registered, replays its current state through <paramref name="replay"/> (or the wrapper itself).
		/// </summary>
		/// <remarks>
		/// The replay runs through the same per-subscriber gate as every notification, so a dispatch on
		/// another thread cannot slip an older state in after it. Exceptions from the replay propagate to
		/// the caller of Subscribe, as before.
		/// </remarks>
		private IDisposable SubscribeRaw<TState>(Action<TState> wrapper, Action<TState>? replay = null) where TState : class, new() {
			var stateType = typeof(TState);
			var entry = new SubscriberEntry(wrapper);

			lock (this.subscribers) {
				if (!this.subscribers.TryGetValue(stateType, out var list)) {
					this.subscribers.Add(stateType, list = []);
				}

				list.Add(entry);
			}

			var subscription = new Subscription(() => {
				// Deactivate first: a notification already snapshotted or posted must not reach a
				// subscriber that has been disposed (e.g. a component torn down mid-dispatch).
				entry.Deactivate();

				lock (this.subscribers) {
					if (this.subscribers.TryGetValue(stateType, out var list)) {
						list.Remove(entry);
						if (list.Count == 0) {
							this.subscribers.Remove(stateType);
						}
					}
				}
			});

			var feature = this.GetFeature<TState>();
			if (feature is not null) {
				try {
					lock (entry.Gate) {
						(replay ?? wrapper).Invoke(feature.State);
					}
				} catch {
					// The caller never receives the subscription, so it could never dispose it.
					subscription.Dispose();
					throw;
				}
			}

			return subscription;
		}

		public IDisposable Subscribe<TState>(Action<TState> callback) where TState : class, new() {
			ArgumentNullException.ThrowIfNull(callback);

			return this.SubscribeRaw<TState>(callback);
		}

		public IDisposable Subscribe<TState, TResult>(
			ISelector<TState, TResult> selector,
			Action<TResult> callback
		) where TState : class, new() {
			ArgumentNullException.ThrowIfNull(selector);
			ArgumentNullException.ThrowIfNull(callback);

			TResult? prev = default;
			var hasPrev = false;

			// The first call (the replay, when the feature exists) always delivers because hasPrev is false.
			return this.SubscribeRaw<TState>(state => {
				var next = selector.Select(state);
				if (!hasPrev || !ReferenceEquals(prev, next)) {
					prev = next;
					hasPrev = true;
					callback(next);
				}
			});
		}

		public IDisposable Subscribe<TState, TResult>(
			ISelector<TState, TResult> selector,
			Func<TResult, Task> callback
		) where TState : class, new() {
			ArgumentNullException.ThrowIfNull(selector);
			ArgumentNullException.ThrowIfNull(callback);

			TResult? prev = default;
			var hasPrev = false;

			// Async-void at the Action<TState> boundary; Store-side does not try/catch the user Task — FluxComponent owns that boundary.
			return this.SubscribeRaw<TState>(
				async state => {
					var next = selector.Select(state);
					if (!hasPrev || !ReferenceEquals(prev, next)) {
						prev = next;
						hasPrev = true;
						await callback(next);
					}
				},
				replay: state => {
					var initial = selector.Select(state);
					prev = initial;
					hasPrev = true;
					// Initial replay is fire-and-forget to match the sync overload's "replay invokes immediately" contract.
					_ = callback(initial);
				}
			);
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

		public ISelector<TState, TResult> Select<TState, TArg, TResult>(
			Func<TState, TArg, TResult> projector,
			TArg arg
		) where TState : class, new() {
			return this.Select<TState, TArg, TResult>(projector, arg, EqualityComparer<TArg>.Default);
		}

		public ISelector<TState, TResult> Select<TState, TArg, TResult>(
			Func<TState, TArg, TResult> projector,
			TArg arg,
			IEqualityComparer<TArg> argComparer
		) where TState : class, new() {
			ArgumentNullException.ThrowIfNull(projector);
			ArgumentNullException.ThrowIfNull(argComparer);

			// MethodHandle.Value is a stable runtime token; Delegate.GetHashCode hashes only the delegate type and would collapse the cache to one bucket.
			var key = new ParameterizedSelectorKey(
				projector.Method.MethodHandle,
				projector.Target,
				arg,
				argComparer,
				new ErasedComparer<TArg>(argComparer)
			);

			return (ISelector<TState, TResult>)this.parameterizedCache.GetOrAdd(
				key,
				static (k, ctx) => new MemoizedSelector<TState, TResult>(
					state => ctx.projector(state, ctx.arg),
					ReferenceEqualityComparer.Instance,
					EqualityComparer<TResult>.Default
				),
				(projector, arg)
			);
		}

		public IComposedSelector<TResult> Select<TState1, TState2, TInput1, TInput2, TResult>(
			ISelector<TState1, TInput1> selectorA,
			ISelector<TState2, TInput2> selectorB,
			Func<TInput1, TInput2, TResult> combiner
		) where TState1 : class, new()
		  where TState2 : class, new() {
			ArgumentNullException.ThrowIfNull(selectorA);
			ArgumentNullException.ThrowIfNull(selectorB);
			ArgumentNullException.ThrowIfNull(combiner);

			return new ComposedSelector<TState1, TState2, TInput1, TInput2, TResult>(
				this, selectorA, selectorB, combiner
			);
		}

		private readonly struct ParameterizedSelectorKey : IEquatable<ParameterizedSelectorKey> {
			private readonly RuntimeMethodHandle projectorMethod;
			private readonly Object? projectorTarget;
			private readonly Object? arg;
			// Compared by reference: two distinct comparer instances produce distinct cache slots even when functionally equivalent.
			private readonly Object argComparerIdentity;
			private readonly IEqualityComparer<Object?> argEqualityDispatch;
			private readonly Int32 hashCode;

			public ParameterizedSelectorKey(
				RuntimeMethodHandle projectorMethod,
				Object? projectorTarget,
				Object? arg,
				Object argComparerIdentity,
				IEqualityComparer<Object?> argEqualityDispatch
			) {
				this.projectorMethod = projectorMethod;
				this.projectorTarget = projectorTarget;
				this.arg = arg;
				this.argComparerIdentity = argComparerIdentity;
				this.argEqualityDispatch = argEqualityDispatch;
				this.hashCode = HashCode.Combine(
					projectorMethod.Value,
					RuntimeHelpers.GetHashCode(projectorTarget!),
					RuntimeHelpers.GetHashCode(argComparerIdentity),
					arg is null ? 0 : argEqualityDispatch.GetHashCode(arg)
				);
			}

			public Boolean Equals(ParameterizedSelectorKey other) =>
				this.projectorMethod.Value == other.projectorMethod.Value
				&& ReferenceEquals(this.projectorTarget, other.projectorTarget)
				&& ReferenceEquals(this.argComparerIdentity, other.argComparerIdentity)
				&& this.argEqualityDispatch.Equals(this.arg, other.arg);

			public override Boolean Equals(Object? obj) => obj is ParameterizedSelectorKey other && this.Equals(other);

			public override Int32 GetHashCode() => this.hashCode;
		}

		private sealed class ErasedComparer<T> : IEqualityComparer<Object?> {
			private readonly IEqualityComparer<T> inner;

			public ErasedComparer(IEqualityComparer<T> inner) {
				this.inner = inner;
			}

			public new Boolean Equals(Object? x, Object? y) {
				if (x is null && y is null) {
					return true;
				}
				if (x is null || y is null) {
					return false;
				}
				return this.inner.Equals((T)x, (T)y);
			}

			public Int32 GetHashCode(Object? obj) => obj is null ? 0 : this.inner.GetHashCode((T)obj);
		}

		private void NotifySubscribers<TState>(IFeature<TState> feature) where TState : class, new() {
			var stateType = typeof(TState);

			// Read the ambient context once per call, before the lock — it's a thread-local read.
			var current = SynchronizationContext.Current;

			// Snapshot under the lock and call out after it. Calling subscribers while holding the lock let a
			// subscriber that (un)subscribes on the same thread — a render mounting or removing a component —
			// modify the list mid-loop ("Collection was modified"), and every later subscriber missed the change.
			SubscriberEntry[] snapshot;
			lock (this.subscribers) {
				if (!this.subscribers.TryGetValue(stateType, out var list) || list.Count == 0) {
					this.logger.LogDebug("No subscribers found for '{StateType}'.", stateType.FullName);
					return;
				}

				snapshot = [.. list];
			}

			foreach (var entry in snapshot) {
				if (current != null) {
					this.logger.LogDebug("Notifying subscriber for '{StateType}' via SynchronizationContext.", stateType.FullName);
					current.Post(_ => this.Deliver(entry, feature), null);
				} else {
					this.logger.LogDebug("Notifying subscriber for '{StateType}' directly (no SynchronizationContext).", stateType.FullName);
					this.Deliver(entry, feature);
				}
			}
		}

		/// <summary>
		/// Hands one subscriber the feature's current state.
		/// </summary>
		/// <remarks>
		/// Deliveries to one subscriber are serialized and each reads the state inside the gate, so the last
		/// delivery a subscriber sees is never older than an earlier one. A throwing subscriber is logged and
		/// does not stop the other subscribers, or the action's effects, from running.
		/// </remarks>
		private void Deliver<TState>(SubscriberEntry entry, IFeature<TState> feature) where TState : class, new() {
			lock (entry.Gate) {
				if (!entry.IsActive) {
					return;
				}

				try {
					((Action<TState>)entry.Callback).Invoke(feature.State);
				} catch (Exception ex) {
					this.logger.LogError(ex, "A subscriber to '{StateType}' threw while being notified.", typeof(TState).FullName);
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
				reduce(action);
			}

			foreach (var (stateType, effect) in this.effects) {
				this.RunEffect(stateType, effect, action);
			}
		}

		/// <summary>
		/// Starts an effect without blocking the dispatch, and makes sure its failure is logged.
		/// </summary>
		/// <remarks>
		/// Effects used to run as async-void lambdas: a throw went unobserved, or was rethrown on the thread
		/// pool or the renderer's context and took the process or circuit down with it.
		/// </remarks>
		private void RunEffect(Type stateType, Func<IAction, Task> effect, IAction action) {
			Task task;

			try {
				task = effect(action);
			} catch (Exception ex) {
				this.LogEffectFailure(ex, stateType, action);
				return;
			}

			if (task.IsCompleted) {
				if (task.IsFaulted) {
					this.LogEffectFailure(task.Exception!, stateType, action);
				}

				return;
			}

			_ = task.ContinueWith(
				static (completed, state) => {
					var (store, stateType, action) = ((Store, Type, IAction))state!;
					store.LogEffectFailure(completed.Exception!, stateType, action);
				},
				(this, stateType, action),
				CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default
			);
		}

		private void LogEffectFailure(Exception exception, Type stateType, IAction action) {
			var failure = exception is AggregateException { InnerExceptions.Count: 1 } aggregate
				? aggregate.InnerExceptions[0]
				: exception;

			this.logger.LogError(failure, "Effect of feature '{StateType}' failed for action {ActionType}", stateType.FullName, action.GetType());
		}

		/// <summary>
		/// One subscriber's callback, plus the gate that serializes its deliveries and the flag that stops
		/// deliveries once its subscription is disposed.
		/// </summary>
		private sealed class SubscriberEntry {
			private volatile Boolean isActive = true;

			public SubscriberEntry(Delegate callback) {
				this.Callback = callback;
			}

			public Delegate Callback { get; }

			// A Monitor (reentrant): a subscriber that dispatches inline is delivered to again on the same thread.
			public Object Gate { get; } = new();

			public Boolean IsActive => this.isActive;

			public void Deactivate() => this.isActive = false;
		}
	}
}
