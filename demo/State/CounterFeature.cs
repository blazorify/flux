using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Demo.State {
	public class CounterFeature : FeatureBase<CounterState> {
		public CounterFeature(
			IStore store
		) : base(store) {
		}

		protected override void ConfigureReducers(ReducerBuilder builder) {
			builder.On<CounterActions.Increment>((state, action) => state with {
				CurrentCount = state.CurrentCount + 1,
			});

			builder.On<CounterActions.Decrement>((state, action) => state with {
				CurrentCount = state.CurrentCount - 1,
			});
		}

		protected override void ConfigureEffects(EffectBuilder builder) {
			// No effects
		}
	}
}
