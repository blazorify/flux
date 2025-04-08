using Blazorify.Flux.Interfaces;
using Microsoft.Extensions.Logging;

namespace Blazorify.Flux.Components {
	public class StoreInitializer : FluxComponent {
		private readonly IStore store;
		private readonly ILogger<StoreInitializer> logger;

		public StoreInitializer(
			IStore store,
			ILogger<StoreInitializer> logger
		) {
			this.store = store;
			this.logger = logger;
		}

		protected override void OnInitialized() {
			base.OnInitialized();

			this.logger.LogDebug("Initializing store...");
			this.store.Initialize();
		}
	}
}
