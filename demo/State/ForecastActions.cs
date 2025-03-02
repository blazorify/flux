using System;
using Blazorify.Flux.Demo.Data;
using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Demo.State {
	public class ForecastActions {
		public record GetForecast : IAction {
		}

		public record GetForecastSuccess : IAction {
			public WeatherForecast[] Forecasts { get; set; } = [];
		}

		public record GetForecastFailure : IAction {
			public Exception? Exception { get; set; } = null;
		}
	}
}
