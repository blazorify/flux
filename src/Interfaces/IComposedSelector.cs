namespace Blazorify.Flux.Interfaces {
	public interface IComposedSelector<TResult> {
		TResult Select();
		TResult LastResult { get; }
		void Reset();
	}
}
