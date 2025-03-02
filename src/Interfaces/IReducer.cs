namespace Blazorify.Flux.Interfaces {
	/// <summary>
	/// Defines a reducer that handles state changes based on dispatched actions.
	/// </summary>
	/// <typeparam name="TState">The type of state managed by this reducer.</typeparam>
	public interface IReducer<TState> where TState : class, new() {
		public TState Reduce(TState state, IAction action);
	}
}
