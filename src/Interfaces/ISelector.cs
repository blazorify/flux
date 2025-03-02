namespace Blazorify.Flux.Interfaces {
	/// <summary>
	/// Provides a mechanism to select and project state slices.
	/// </summary>
	/// <typeparam name="TState">The type of state being selected from.</typeparam>
	/// <typeparam name="TResult">The type of the selected result.</typeparam>
	public interface ISelector<TState, TResult> {
		TResult Select(TState state);
	}
}