using Blazorify.Flux.Demo.Data;
using Blazorify.Flux.Demo.Workers;

namespace Microsoft.Extensions.DependencyInjection {
	public static class AddDemoExtension {
		public static IServiceCollection AddDemo(this IServiceCollection services) {
			services.AddSingleton<WeatherForecastService>();

			services.AddHostedService<ForecastWorker>();

			return services;
		}
	}
}
