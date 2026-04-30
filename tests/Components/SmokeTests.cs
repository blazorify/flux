using Bunit;
using Xunit;

namespace Blazorify.Flux.Tests.Components;

public class SmokeTests {
	[Fact]
	public void Smoke_BunitContext_CanBeInstantiated() {
		using var ctx = new BunitContext();
		Assert.NotNull(ctx);
	}
}
