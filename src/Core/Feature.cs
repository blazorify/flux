using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Core {
	public abstract class Feature<TState> : IFeature<TState> where TState : class, new() {
		protected readonly IStore store;
		private TState state = new();

		protected readonly Dictionary<Type, List<IReducer<TState>>> reducers = [];
		protected readonly Dictionary<Type, List<Func<IAction, Task<IAction>>>> effects = [];

		public virtual String Name {
			get => this.GetType().Name;
		}

		public virtual TState State {
			get => this.state;
		}

		public virtual IEnumerable<IReducer<TState>> Reducers {
			get => this.reducers.SelectMany(m => m.Value);
		}

		protected Feature(
			IStore store
		) {
			this.store = store;

			this.ConfigureReducers(new((type, reducer) => {
				if (!this.reducers.TryGetValue(type, out var reducers)) {
					this.reducers.Add(type, reducers = []);
				}

				reducers.Add(reducer);
			}));

			this.ConfigureEffects(new((type, effect) => {
				if (!this.effects.TryGetValue(type, out var effects)) {
					this.effects.Add(type, effects = []);
				}

				effects.Add(effect);
			}));

			this.store.AddFeature(this);
		}

		protected abstract void ConfigureReducers(ReducerBuilder builder);

		protected abstract void ConfigureEffects(EffectBuilder builder);

		public void Reduce(IAction action, Action<TState> callback) {
			if (!this.reducers.TryGetValue(action.GetType(), out var reducers)) {
				return;
			}

			foreach (var reducer in reducers) {
				var state = reducer.Reduce(this.state, action);

				if (!ReferenceEquals(this.state, state)) {
					callback(this.state = state);
				}
			}
		}

		public async Task Effect(IAction action) {
			if (!this.effects.TryGetValue(action.GetType(), out var effects)) {
				return;
			}

			foreach (var effect in effects) {
				var result = await effect.Invoke(action);

				this.store.Dispatch(result);
			}
		}

		protected class ReducerBuilder {
			private readonly Action<Type, IReducer<TState>> register;

			public ReducerBuilder(Action<Type, IReducer<TState>> register) {
				this.register = register;
			}

			public ReducerBuilder On<TAction>(Func<TState, TAction, TState> reduce) where TAction : IAction {
				this.register.Invoke(typeof(TAction), new Reducer<TState>((state, action) => reduce(state, (TAction)action)));

				return this;
			}
		}

		protected class EffectBuilder {
			private readonly Action<Type, Func<IAction, Task<IAction>>> register;

			public EffectBuilder(Action<Type, Func<IAction, Task<IAction>>> register) {
				this.register = register;
			}

			public EffectBuilder On<TAction>(Func<TAction, IAction> effect) where TAction : IAction {
				return this.On<TAction>(action => Task.FromResult(effect(action)));
			}

			public EffectBuilder On<TAction>(Func<TAction, Task<IAction>> effect) where TAction : IAction {
				this.register.Invoke(typeof(TAction), action => effect((TAction)action));

				return this;
			}
		}
	}
}
