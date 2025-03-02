using System.Reflection;

namespace Blazorify.Flux.Options {
	public class BlazorifyFluxOptions {
		public Assembly[] Assemblies { get; set; } = [
			Assembly.GetEntryAssembly()!,
		];
	}
}
