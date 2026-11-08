using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Options;
using Blazorify.Flux.Tests.Components.Fixtures;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blazorify.Flux.Tests.Components;

public class FluxComponentDisposalTests : BunitContext {

	public FluxComponentDisposalTests() {
		this.Services.AddSingleton<IStore>(sp => new Store(
			Microsoft.Extensions.Options.Options.Create(new BlazorifyFluxOptions()),
			sp,
			NullLogger<Store>.Instance));
		this.Services.AddSingleton<IDispatcher>(sp => new Dispatcher(
			sp.GetRequiredService<IStore>(),
			NullLogger<Dispatcher>.Instance));
		this.Services.AddSingleton(NullLogger<Blazorify.Flux.Components.FluxComponent>.Instance);
	}

	[Fact]
	public async Task DisposeAsync_WhenOverridden_RendererCallsOverrideAndBaseReleasesSubscriptions() {
		var store = (Store)this.Services.GetRequiredService<IStore>();
		store.AddFeature<ComponentTestState>(new ComponentTestFeature());

		var cut = this.Render<OverridingDisposeComponent>();
		var instance = cut.Instance;
		Assert.True(store.HasSubscriberEntry(typeof(ComponentTestState)));

		await this.DisposeComponentsAsync();

		Assert.True(instance.OwnResourcesReleased);
		Assert.False(store.HasSubscriberEntry(typeof(ComponentTestState)));
	}

	[Fact]
	public async Task Dispatch_AfterDispose_DisposedComponentIsNotNotified() {
		var store = this.Services.GetRequiredService<IStore>();
		var dispatcher = this.Services.GetRequiredService<IDispatcher>();
		store.AddFeature<ComponentTestState>(new ComponentTestFeature());

		var cut = this.Render<OverridingDisposeComponent>();
		var instance = cut.Instance;

		await this.DisposeComponentsAsync();
		dispatcher.Dispatch(new ComponentTestActions.Increment());

		Assert.Equal(0, instance.LastObservedCounter);
	}

	[Fact]
	public async Task Subscribe_WhenSyncCallbackThrows_ComponentKeepsRenderingAndOthersAreNotified() {
		var store = this.Services.GetRequiredService<IStore>();
		var dispatcher = this.Services.GetRequiredService<IDispatcher>();
		store.AddFeature<ComponentTestState>(new ComponentTestFeature());

		var throwing = this.Render<ThrowingSubscriberComponent>();
		var sibling = this.Render<TestSubscriberComponent>();
		var rendersBefore = throwing.Instance.Renders;

		await throwing.InvokeAsync(() =>
			dispatcher.Dispatch(new ComponentTestActions.Increment()));

		sibling.WaitForAssertion(() => Assert.Equal(1, sibling.Instance.LastObservedCounter));
		throwing.WaitForAssertion(() => Assert.True(throwing.Instance.Renders > rendersBefore));
	}
}
