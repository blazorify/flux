using System;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace Blazorify.Flux.Core {
	public class State<TState> where TState : class, new() {
		private ImmutableDictionary<String, Object?> state = ImmutableDictionary<String, Object?>.Empty;

		public State() : this(new TState()) {
		}

		public State(TState state) {
			this.Set(state);
		}

		public Boolean ApplyChanges(TState newState, out TState state) {
			var changed = false;

			foreach (var property in typeof(TState).GetProperties()) {
				if (this.state.TryGetValue(property.Name, out var oldValue)) {
					var newValue = property.GetValue(newState);

					if (!this.state.ValueComparer.Equals(newValue, oldValue)) {
						changed = true;

						this.state = this.state.SetItem(property.Name, newValue);
					}
				}
			}

			state = this.Get();

			return changed;
		}

		public TState Get() {
			var state = new TState();

			foreach (var property in typeof(TState).GetProperties()) {
				if (this.state.TryGetValue(property.Name, out var value)) {
					property.SetValue(state, value);
				}
			}

			return state;
		}

		public TSlice Get<TSlice>(Expression<Func<TState, TSlice>> selector) where TSlice : class, new() {
			if (selector.Body is not MemberExpression member) {
				throw new InvalidOperationException("Invalid selector expression.");
			}

			if (!this.state.TryGetValue(member.Member.Name, out var value) || value is not TSlice typedValue) {
				throw new InvalidOperationException($"State slice '{member.Member.Name}' is not registered.");
			}

			return typedValue;
		}

		public TState Set(TState state) {
			this.state = ImmutableDictionary<String, Object?>.Empty;

			foreach (var property in typeof(TState).GetProperties()) {
				this.state = this.state.SetItem(property.Name, property.GetValue(state));
			}

			return this.Get();
		}

		public State<TState> Set<TSlice>(Expression<Func<TState, TSlice>> selector, TSlice value) where TSlice : class, new() {
			if (selector.Body is not MemberExpression member) {
				throw new InvalidOperationException("Invalid selector expression.");
			}

			this.state = this.state.SetItem(member.Member.Name, value);
			return this;
		}

		public TResult Select<TResult>(Func<TState, TResult> selector) {
			return selector(new TState());
		}
	}
}
