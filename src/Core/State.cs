using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Blazorify.Flux.Core {
	public class State<TState> where TState : class, new() {
		private readonly Lock syncRoot = new();

		private ImmutableDictionary<String, Object?> state = ImmutableDictionary<String, Object?>.Empty;

		private TState cachedState;

		private static readonly ConcurrentDictionary<Type, PropertyInfo[]> cachedProperties = new();

		public State() : this(new TState()) {
		}

		public State(TState state) {
			this.cachedState = default!;
			this.Set(state);
		}

		public Boolean ApplyChanges(TState newState, out TState state) {
			lock (this.syncRoot) {
				var changed = false;

				foreach (var property in cachedProperties.GetOrAdd(typeof(TState), static type => type.GetProperties())) {
					if (this.state.TryGetValue(property.Name, out var oldValue)) {
						var newValue = property.GetValue(newState);

						if (!this.state.ValueComparer.Equals(newValue, oldValue)) {
							changed = true;

							this.state = this.state.SetItem(property.Name, newValue);
						}
					}
				}

				if (changed) {
					this.cachedState = this.RehydrateLocked();
				}

				state = this.cachedState;

				return changed;
			}
		}

		public Boolean Transform(Func<TState, TState> reducer, out TState newState) {
			ArgumentNullException.ThrowIfNull(reducer);

			// Reducer must run inside the lock.
			// When run outside, two threads can compute against the same snapshot and lose updates.
			// Safe because reducers are pure and synchronous per Flux contract.
			lock (this.syncRoot) {
				var candidate = reducer(this.cachedState);
				var changed = false;

				foreach (var property in cachedProperties.GetOrAdd(typeof(TState), static type => type.GetProperties())) {
					if (this.state.TryGetValue(property.Name, out var oldValue)) {
						var newValue = property.GetValue(candidate);

						if (!this.state.ValueComparer.Equals(newValue, oldValue)) {
							changed = true;

							this.state = this.state.SetItem(property.Name, newValue);
						}
					}
				}

				if (changed) {
					this.cachedState = this.RehydrateLocked();
				}

				newState = this.cachedState;

				return changed;
			}
		}

		public TState Get() {
			lock (this.syncRoot) {
				return this.cachedState;
			}
		}

		public TSlice Get<TSlice>(Expression<Func<TState, TSlice>> selector) where TSlice : class, new() {
			if (selector.Body is not MemberExpression member) {
				throw new InvalidOperationException("Invalid selector expression.");
			}

			lock (this.syncRoot) {
				if (!this.state.TryGetValue(member.Member.Name, out var value) || value is not TSlice typedValue) {
					throw new InvalidOperationException($"State slice '{member.Member.Name}' is not registered.");
				}

				return typedValue;
			}
		}

		public TState Set(TState state) {
			lock (this.syncRoot) {
				this.state = ImmutableDictionary<String, Object?>.Empty;

				foreach (var property in cachedProperties.GetOrAdd(typeof(TState), static type => type.GetProperties())) {
					this.state = this.state.SetItem(property.Name, property.GetValue(state));
				}

				this.cachedState = this.RehydrateLocked();

				return this.cachedState;
			}
		}

		public State<TState> Set<TSlice>(Expression<Func<TState, TSlice>> selector, TSlice value) where TSlice : class, new() {
			if (selector.Body is not MemberExpression member) {
				throw new InvalidOperationException("Invalid selector expression.");
			}

			lock (this.syncRoot) {
				this.state = this.state.SetItem(member.Member.Name, value);
				this.cachedState = this.RehydrateLocked();

				return this;
			}
		}

		/// <summary>
		/// Rebuilds the cached TState reference from the current ImmutableDictionary using the cached PropertyInfo[].
		///
		/// NOTE: Caller MUST hold syncRoot.
		/// </summary>
		private TState RehydrateLocked() {
			var instance = new TState();

			foreach (var property in cachedProperties.GetOrAdd(typeof(TState), static type => type.GetProperties())) {
				if (this.state.TryGetValue(property.Name, out var value) && property.CanWrite) {
					property.SetValue(instance, value);
				}
			}

			return instance;
		}
	}
}
