using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Tests.Components.Fixtures;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blazorify.Flux.Tests.Components;

public class StoreInitializerTests : BunitContext {

	public StoreInitializerTests() {
		this.Services.AddSingleton<ILogger<Store>>(NullLogger<Store>.Instance);
		this.Services.AddSingleton<ILogger<Dispatcher>>(NullLogger<Dispatcher>.Instance);
		this.Services.AddSingleton<ILogger<Blazorify.Flux.Components.FluxComponent>>(NullLogger<Blazorify.Flux.Components.FluxComponent>.Instance);
		this.Services.AddSingleton<ILogger<Blazorify.Flux.Components.StoreInitializer>>(NullLogger<Blazorify.Flux.Components.StoreInitializer>.Instance);
		this.Services.AddBlazorifyFlux(opts => {
			opts.Assemblies = [typeof(DiscoverableComponentFeature).Assembly];
		});
	}

	[Fact]
	public void Render_StoreInitializer_TriggersInitializeAndRegistersDiscoveredFeatures() {
		var store = this.Services.GetRequiredService<IStore>();
		Assert.Null(store.GetFeature<DiscoverableComponentState>());

		this.Render<Blazorify.Flux.Components.StoreInitializer>();

		Assert.NotNull(store.GetFeature<DiscoverableComponentState>());
	}
}
