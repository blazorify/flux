using System;
using Blazorify.Flux.Demo.Data;

namespace Blazorify.Flux.Demo.State {
	public record ForecastState {
		public Boolean Loading { get; set; } = false;

		public WeatherForecast[] Forecasts { get; set; } = [];

		public Exception? Exception { get; set; } = null;
	}
}
