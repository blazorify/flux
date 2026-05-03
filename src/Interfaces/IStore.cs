using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

		public ISelector<TState, TResult> Select<TState, TArg, TResult>(
			Func<TState, TArg, TResult> projector,
			TArg arg
		) where TState : class, new();

		public ISelector<TState, TResult> Select<TState, TArg, TResult>(
			Func<TState, TArg, TResult> projector,
			TArg arg,
			IEqualityComparer<TArg> argComparer
		) where TState : class, new();

		public IComposedSelector<TResult> Select<TState1, TState2, TInput1, TInput2, TResult>(
			ISelector<TState1, TInput1> selectorA,
			ISelector<TState2, TInput2> selectorB,
			Func<TInput1, TInput2, TResult> combiner
		) where TState1 : class, new()
		  where TState2 : class, new();

		public IDisposable Subscribe<TState, TResult>(
			ISelector<TState, TResult> selector,
			Action<TResult> callback
		) where TState : class, new();

		public IDisposable Subscribe<TState, TResult>(
			ISelector<TState, TResult> selector,
			Func<TResult, Task> callback
		) where TState : class, new();

		internal void ProcessAction(IAction action);
	}
}
