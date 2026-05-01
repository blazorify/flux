using Blazorify.Flux.Core;

namespace Blazorify.Flux.Tests.Integration.Fixtures;

public class IntegrationTestFeature : Feature<IntegrationTestState> {
	protected override void ConfigureReducers(ReducerBuilder builder) {
		builder.On<IntegrationTestActions.Increment>((state, _) => state with {
			Counter = state.Counter + 1,
		});

		builder.On<IntegrationTestActions.IncrementBy>((state, action) => state with {
			Counter = state.Counter + action.Amount,
		});

		builder.On<IntegrationTestActions.SetName>((state, action) => state with {
			Name = action.Value,
		});

		builder.On<IntegrationTestActions.AsyncWorkCompleted>((state, action) => state with {
			Counter = state.Counter + action.Result,
			LastEffectMarker = "AsyncWorkCompleted",
		});
	}

	protected override void ConfigureEffects(EffectBuilder builder) {
		builder.On<IntegrationTestActions.StartAsyncWork>(async (dispatcher, _) => {
			await Task.Yield();
			dispatcher.Dispatch(new IntegrationTestActions.AsyncWorkCompleted(10));
		});
	}
}
