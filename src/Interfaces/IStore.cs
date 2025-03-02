using System;

namespace Blazorify.Flux.Interfaces {
	/// <summary>
	/// Defines the contract for the central store managing application state.
	/// </summary>
	public interface IStore {
		public void AddFeature<TState>(IFeature<TState> feature) where TState : class, new();

		//public void AddReducer<TState, TAction>(IReducer<TState> reducer) where TState : class, new() where TAction : IAction;

		public IDisposable Subscribe(Action callback);

		public IDisposable Subscribe<TState>(Action<TState> callback) where TState : class, new();

		public void Dispatch<TAction>() where TAction : IAction, new();

		public void Dispatch(IAction action);

		//public TState Select<TState>() where TState : class, new();

		//public TResult Select<TResult>(Func<IStore, TResult> selector);
	}
}
