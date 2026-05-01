using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blazorify.Flux.Tests.Integration.Fixtures;

public static class IntegrationTestServices {
	public static IServiceProvider BuildWithStore(out Store store, out Dispatcher dispatcher) {
		var services = new ServiceCollection();

		services.AddSingleton<IStore>(sp => new Store(
			Microsoft.Extensions.Options.Options.Create(new BlazorifyFluxOptions()),
			sp,
			NullLogger<Store>.Instance
		));

		services.AddSingleton<IDispatcher>(sp => new Dispatcher(
			sp.GetRequiredService<IStore>(),
			NullLogger<Dispatcher>.Instance
		));

		var provider = services.BuildServiceProvider();
		store = (Store)provider.GetRequiredService<IStore>();
		dispatcher = (Dispatcher)provider.GetRequiredService<IDispatcher>();
		return provider;
	}
}
