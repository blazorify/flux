using System.Reflection;
using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blazorify.Flux.Tests.Unit.Fixtures;

public static class TestServices {
	/// <summary>
	/// Builds an IServiceProvider with both IStore and IDispatcher registered as singletons.
	/// The Store receives this same IServiceProvider so its effect lambda can resolve IDispatcher.
	/// </summary>
	public static IServiceProvider BuildWithStore(out Store store, out Dispatcher dispatcher) {
		var services = new ServiceCollection();

		services.AddSingleton<IStore>(sp => new Store(
			Microsoft.Extensions.Options.Options.Create(new BlazorifyFluxOptions()),
			sp,
			NullLogger<Store>.Instance
		));

		services.AddSingleton<IDispatcher>(sp => new Dispatcher(sp.GetRequiredService<IStore>()));

		var provider = services.BuildServiceProvider();
		store = (Store)provider.GetRequiredService<IStore>();
		dispatcher = (Dispatcher)provider.GetRequiredService<IDispatcher>();
		return provider;
	}

	/// <summary>
	/// Minimal IServiceProvider for tests that do NOT trigger Store effects.
	/// Effects throw if the test triggers one (no IDispatcher registered) — by design.
	/// </summary>
	public static IServiceProvider BuildEmpty() =>
		new ServiceCollection().BuildServiceProvider();

	/// <summary>
	/// Wraps the given assemblies in IOptions&lt;BlazorifyFluxOptions&gt; for Store.Initialize tests.
	/// </summary>
	public static IOptions<BlazorifyFluxOptions> Options(params Assembly[] assemblies) =>
		Microsoft.Extensions.Options.Options.Create(
			new BlazorifyFluxOptions { Assemblies = assemblies }
		);
}
