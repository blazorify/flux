using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Blazorify.Flux.Interfaces {
	/// <summary>
	/// Represents a feature module that manages its own slice of state.
	/// </summary>
	/// <typeparam name="TState">The type of state managed by this feature.</typeparam>
	public interface IFeature<TState> where TState : class, new() {
		public String Name { get; }

		public TState State { get; }

		public IEnumerable<IReducer<TState>> Reducers { get; }

		public void Reduce(IAction action, Action<TState> callback);

		public Task Effect(IAction action);
	}
}
