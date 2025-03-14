using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Core {
	public abstract class Feature<TState> : IFeature<TState> where TState : class, new() {
		private State<TState> state = new();

		protected readonly Dictionary<Type, List<IReducer<TState>>> reducers = [];
		protected readonly Dictionary<Type, List<Func<IDispatcher, IAction, Task>>> effects = [];

		public virtual String Name {
			get => this.GetType().Name;
		}

		public virtual TState State {
			get => this.state.Get();
		}

		public virtual IEnumerable<IReducer<TState>> Reducers {
			get => this.reducers.SelectMany(m => m.Value);
		}

		protected Feature() {
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
		}

		protected abstract void ConfigureReducers(ReducerBuilder builder);

		protected abstract void ConfigureEffects(EffectBuilder builder);

		public void Reduce(IAction action, Action<TState> callback) {
			if (!this.reducers.TryGetValue(action.GetType(), out var reducers)) {
				return;
			}

			foreach (var reducer in reducers) {
				var oldState = this.state.Get();

				if (this.state.ApplyChanges(reducer.Reduce(oldState, action), out TState newState)) {
					callback(newState);
				}
			}
		}

		public async Task Effect(IDispatcher dispatcher, IAction action) {
			if (!this.effects.TryGetValue(action.GetType(), out var effects)) {
				return;
			}

			foreach (var effect in effects) {
				await effect.Invoke(dispatcher, action);
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
			private readonly Action<Type, Func<IDispatcher, IAction, Task>> register;

			public EffectBuilder(Action<Type, Func<IDispatcher, IAction, Task>> register) {
				this.register = register;
			}

			public EffectBuilder On<TAction>(Action<IDispatcher, TAction> effect) where TAction : IAction {
				return this.On<TAction>((dispatcher, action) => {
					effect(dispatcher, action);

					return Task.CompletedTask;
				});
			}

			public EffectBuilder On<TAction>(Func<IDispatcher, TAction, Task> effect) where TAction : IAction {
				this.register.Invoke(typeof(TAction), (dispatcher, action) => effect(dispatcher, (TAction)action));

				return this;
			}
		}
	}
}
