using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Blazorify.Flux.Interfaces {
	/// <summary>
	/// Represents a feature module that manages its own slice of state.
	/// </summary>
	/// <typeparam name="TState">The type of state managed by this feature.</typeparam>
	public interface IFeature<TState> where TState : class, new() {
		String Name { get; }

		TState State { get; }

		IEnumerable<IReducer<TState>> Reducers { get; }

		void Reduce(IAction action, Action<TState> callback);

		Task Effect(IDispatcher dispatcher, IAction action);
	}
}
