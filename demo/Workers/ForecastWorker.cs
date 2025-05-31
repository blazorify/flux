using System;
using System.Threading;
using System.Threading.Tasks;
using Blazorify.Flux.Demo.State;
using Blazorify.Flux.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blazorify.Flux.Demo.Workers {
	public class ForecastWorker : BackgroundService {
		private readonly ILogger<ForecastWorker> logger;
		private readonly IDispatcher dispatcher;

		public ForecastWorker(
			ILogger<ForecastWorker> logger,
			IDispatcher dispatcher
		) {
			this.logger = logger;
			this.dispatcher = dispatcher;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

			while (!stoppingToken.IsCancellationRequested) {
				this.logger.LogDebug("Dispatching ForecastActions.GetForecast from background worker");
				this.dispatcher.Dispatch<ForecastActions.GetForecast>();

				await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
			}
		}
	}

}
