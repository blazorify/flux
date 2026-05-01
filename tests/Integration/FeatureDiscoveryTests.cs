using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blazorify.Flux.Tests.Integration;

public class FeatureDiscoveryTests {

	// xUnit v2 AsyncTestSyncContext defers Posts past synchronous Asserts; null Current makes notifications fire inline.
	private static void ClearSyncContext() =>
		SynchronizationContext.SetSynchronizationContext(null);

	private static ServiceProvider BuildProvider() {
		var services = new ServiceCollection();
		services.AddSingleton<ILogger<Store>>(NullLogger<Store>.Instance);
		services.AddSingleton<ILogger<Dispatcher>>(NullLogger<Dispatcher>.Instance);
		services.AddBlazorifyFlux(opts => {
			opts.Assemblies = [typeof(DiscoverableFeature).Assembly];
		});
		return services.BuildServiceProvider();
	}

	[Fact]
	public void Initialize_WhenDiscoverableFeatureInScannedAssembly_RegistersFeature() {
		ClearSyncContext();
		using var sp = BuildProvider();
		var store = sp.GetRequiredService<IStore>();

		store.Initialize();

		Assert.NotNull(store.GetFeature<DiscoverableState>());
	}

	[Fact]
	public void Initialize_WhenFailingFeatureCoexistsWithDiscoverable_SkipsFailureRegistersWorking() {
		ClearSyncContext();
		using var sp = BuildProvider();
		var store = sp.GetRequiredService<IStore>();

		store.Initialize();

		Assert.Null(store.GetFeature<FailingDiscoverableState>());
		Assert.NotNull(store.GetFeature<DiscoverableState>());
	}

	[Fact]
	public void Initialize_WhenCalledTwice_IsIdempotent() {
		ClearSyncContext();
		using var sp = BuildProvider();
		var store = sp.GetRequiredService<IStore>();

		store.Initialize();
		var featureAfterFirst = store.GetFeature<DiscoverableState>();

		store.Initialize();

		Assert.Same(featureAfterFirst, store.GetFeature<DiscoverableState>());
	}
}
