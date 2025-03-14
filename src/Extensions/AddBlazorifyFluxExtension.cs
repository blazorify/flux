using System;
using System.Reflection;
using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Options;

namespace Microsoft.Extensions.DependencyInjection {
	public static class AddBlazorifyFluxExtension {
		public static IServiceCollection AddBlazorifyFlux(this IServiceCollection services) {
			return services.AddBlazorifyFlux(options => {
				options.Assemblies = [Assembly.GetEntryAssembly()!];
			});
		}

		public static IServiceCollection AddBlazorifyFlux(this IServiceCollection services, Action<BlazorifyFluxOptions> configure) {
			services.AddSingleton<IStore, Store>();
			services.AddSingleton<IDispatcher, Dispatcher>();

			var options = new BlazorifyFluxOptions();
			configure.Invoke(options);

			return services;
		}
	}
}
