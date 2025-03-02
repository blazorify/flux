using System;
using Blazorify.Flux.Core;
using Blazorify.Flux.Demo.Data;
using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Demo.State {
	public class ForecastFeature : FeatureBase<ForecastState> {
		private readonly WeatherForecastService weatherForecastService;

		public ForecastFeature(
			IStore store,
			WeatherForecastService weatherForecastService
		) : base(store) {
			this.weatherForecastService = weatherForecastService;
		}

		protected override void ConfigureReducers(ReducerBuilder builder) {
			builder.On<ForecastActions.GetForecast>((state, action) => state with {
				Loading = true,
			});

			builder.On<ForecastActions.GetForecastSuccess>((state, action) => state with {
				Loading = false,
				Forecasts = action.Forecasts,
			});

			builder.On<ForecastActions.GetForecastFailure>((state, action) => state with {
				Loading = false,
				Exception = action.Exception,
			});
		}

		protected override void ConfigureEffects(EffectBuilder builder) {
			builder.On<ForecastActions.GetForecast>(async action => {
				try {
					var forecasts = await this.weatherForecastService.GetForecastAsync(DateTime.Now);

					return new ForecastActions.GetForecastSuccess() {
						Forecasts = forecasts,
					};
				} catch(Exception ex) {
					return new ForecastActions.GetForecastFailure() {
						Exception = ex,
					};
				}
			});
		}
	}
}
