using System;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace Blazorify.Flux.Core {
	public class State {
		private readonly ImmutableDictionary<String, Object> state;

		public State() : this(ImmutableDictionary<String, Object>.Empty) {
		}

		private State(ImmutableDictionary<String, Object> state) {
			this.state = state;
		}

		public TSlice Get<TSlice>(Expression<Func<State, TSlice>> selector) where TSlice : class, new() {
			if (selector.Body is not MemberExpression member) {
				throw new InvalidOperationException("Invalid selector expression.");
			}

			if (!this.state.TryGetValue(member.Member.Name, out var value) || value is not TSlice typedValue) {
				throw new InvalidOperationException($"State slice '{member.Member.Name}' is not registered.");
			}

			return typedValue;
		}

		public State Set<TSlice>(Expression<Func<State, TSlice>> selector, TSlice value) where TSlice : class, new() {
			if (selector.Body is not MemberExpression member) {
				throw new InvalidOperationException("Invalid selector expression.");
			}

			return new State(this.state.SetItem(member.Member.Name, value));
		}

		public TResult Select<TResult>(Func<State, TResult> selector) {
			return selector(this);
		}
	}
}
