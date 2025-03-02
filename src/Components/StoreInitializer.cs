using System;
using System.Linq;
using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blazorify.Flux.Components {
	public class StoreInitializer : FluxComponent {
		private readonly IOptions<BlazorifyFluxOptions> optionsAccessor;
		private readonly IStore store;
		private readonly IServiceProvider serviceProvider;
		private readonly ILogger<StoreInitializer> logger;

		public StoreInitializer(
			IOptions<BlazorifyFluxOptions> optionsAccessor,
			IStore store,
			IServiceProvider serviceProvider,
			ILogger<StoreInitializer> logger
		) {
			this.optionsAccessor = optionsAccessor;
			this.store = store;
			this.serviceProvider = serviceProvider;
			this.logger = logger;
		}

		protected override void OnInitialized() {
			base.OnInitialized();

			var featureTypes = this.optionsAccessor.Value.Assemblies
				.SelectMany(assembly => assembly.GetTypes())
				.Where(type => type.BaseType is { IsGenericType: true } && type.BaseType.GetGenericTypeDefinition() == typeof(FeatureBase<>));

			foreach (var featureType in featureTypes) {
				try {
					var feature = ActivatorUtilities.CreateInstance(this.serviceProvider, featureType);

					this.logger.LogDebug("Feature '{featureType}' has been discovered", featureType);

					this.store.AddFeature((dynamic)feature);

					this.logger.LogDebug("Feature '{featureType}' has been added", featureType);
				} catch (Exception ex) {
					this.logger.LogError(ex, ex.Message);
				}
			}
		}
	}
}
