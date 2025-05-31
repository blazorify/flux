using Blazorify.Flux.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder {
	public static class UseDemoExtension {
		public static WebApplication UseDemo(this WebApplication app) {
			using (var scope = app.Services.CreateScope()) {
				scope.ServiceProvider.GetRequiredService<IStore>().Initialize();
			}

			return app;
		}
	}
}
