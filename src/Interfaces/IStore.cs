using System;

namespace Blazorify.Flux.Interfaces {
	/// <summary>
	/// Defines the contract for the central store managing application state.
	/// </summary>
	public interface IStore {
		public void Initialize();

		public void AddFeature<TState>(IFeature<TState> feature) where TState : class, new();

		public IDisposable Subscribe<TState>(Action<TState> callback) where TState : class, new();

		internal void ProcessAction(IAction action);
	}
}
