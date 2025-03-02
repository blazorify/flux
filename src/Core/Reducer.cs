using System;
using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Core {
	public class Reducer<TState> : IReducer<TState> where TState : class, new() {
		private Func<TState, IAction, TState> reduce;

		public Reducer(Func<TState, IAction, TState> reduce) {
			this.reduce = reduce;
		}

		public TState Reduce(TState state, IAction action) {
			return this.reduce.Invoke(state, action);
		}
	}
}
