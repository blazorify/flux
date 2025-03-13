using Blazorify.Flux.Core;

namespace Blazorify.Flux.Demo.State {
	public class CounterFeature : Feature<CounterState> {
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
