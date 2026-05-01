using System;
using System.Collections.Generic;

namespace Blazorify.Flux.Interfaces {
	/// <summary>
	/// Defines the contract for the central store managing application state.
	/// </summary>
	public interface IStore {
		public void Initialize();

		public void AddFeature<TState>(IFeature<TState> feature) where TState : class, new();

		public IFeature<TState>? GetFeature<TState>() where TState : class, new();

		public IDisposable Subscribe<TState>(Action<TState> callback) where TState : class, new();

		public ISelector<TState, TResult> Select<TState, TResult>(
			Func<TState, TResult> projector
		) where TState : class, new();

		public ISelector<TState, TResult> Select<TState, TResult>(
			Func<TState, TResult> projector,
			IEqualityComparer<TState> inputComparer
		) where TState : class, new();

		public ISelector<TState, TResult> Select<TState, TResult>(
			Func<TState, TResult> projector,
			IEqualityComparer<TState> inputComparer,
			IEqualityComparer<TResult> outputComparer
		) where TState : class, new();

		internal void ProcessAction(IAction action);
	}
}
