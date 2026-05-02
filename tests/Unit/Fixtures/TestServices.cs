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

		services.AddSingleton<IDispatcher>(sp => new Dispatcher(
			sp.GetRequiredService<IStore>(),
			NullLogger<Dispatcher>.Instance
		));

		var provider = services.BuildServiceProvider();
		store = (Store)provider.GetRequiredService<IStore>();
		dispatcher = (Dispatcher)provider.GetRequiredService<IDispatcher>();
		return provider;
	}

	/// <summary>
	/// Builds an IServiceProvider with IStore and IDispatcher registered, then invokes
	/// each registration callback against the resolved Store. Bypasses Store.Initialize's
	/// assembly scan so tests register only the features they need (and skip throwing
	/// fixtures like FailingFeature that would otherwise pollute test diagnostics).
	/// </summary>
	public static IServiceProvider BuildWithStoreAndFeatures(
		out Store store,
		out Dispatcher dispatcher,
		params Action<Store>[] featureRegistrations
	) {
		var provider = BuildWithStore(out store, out dispatcher);
		foreach (var register in featureRegistrations) {
			register(store);
		}
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
