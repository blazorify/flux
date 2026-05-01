using Blazorify.Flux.Core;
using Blazorify.Flux.Interfaces;
using Blazorify.Flux.Options;
using Blazorify.Flux.Tests.Components.Fixtures;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Blazorify.Flux.Tests.Components;

public class FluxComponentTests : BunitContext {

	public FluxComponentTests() {
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
	public void Render_WhenSubscribed_ComponentReceivesInitialStateViaReplay() {
		var store = this.Services.GetRequiredService<IStore>();
		store.AddFeature<ComponentTestState>(new ComponentTestFeature());

		var cut = this.Render<TestSubscriberComponent>();

		Assert.Equal(0, cut.Instance.LastObservedCounter);
	}

	[Fact]
	public async Task Dispatch_AfterRender_ComponentRerendersWithNewState() {
		var store = this.Services.GetRequiredService<IStore>();
		var dispatcher = this.Services.GetRequiredService<IDispatcher>();
		store.AddFeature<ComponentTestState>(new ComponentTestFeature());

		var cut = this.Render<TestSubscriberComponent>();
		await cut.InvokeAsync(() =>
			dispatcher.Dispatch(new ComponentTestActions.Increment()));

		cut.WaitForAssertion(() => Assert.Equal(1, cut.Instance.LastObservedCounter));
	}

	[Fact]
	public async Task DisposeAsync_AfterSubscribe_RemovesSubscriberEntryFromStore() {
		var store = (Store)this.Services.GetRequiredService<IStore>();
		store.AddFeature<ComponentTestState>(new ComponentTestFeature());

		var cut = this.Render<TestSubscriberComponent>();
		Assert.True(store.HasSubscriberEntry(typeof(ComponentTestState)));

		await this.DisposeComponentsAsync();

		Assert.False(store.HasSubscriberEntry(typeof(ComponentTestState)));
	}

	[Fact]
	public async Task AsyncCallback_WhenUserCallbackThrows_DoesNotPropagate_StateHasChangedStillRuns() {
		var store = this.Services.GetRequiredService<IStore>();
		var dispatcher = this.Services.GetRequiredService<IDispatcher>();
		store.AddFeature<ComponentTestState>(new ComponentTestFeature());

		var cut = this.Render<AsyncSubscriberComponent>(p => p
			.Add(c => c.ThrowOnCallback, true));

		await cut.InvokeAsync(() =>
			dispatcher.Dispatch(new ComponentTestActions.Increment()));

		cut.WaitForAssertion(() => Assert.True(cut.RenderCount >= 2));

		await this.DisposeComponentsAsync();
		Assert.False(this.Renderer.UnhandledException.IsCompleted);
	}

	[Fact]
	public async Task Subscribe_WhenTwoSiblingsRender_BothObserveTheSameStateSlice() {
		var store = this.Services.GetRequiredService<IStore>();
		var dispatcher = this.Services.GetRequiredService<IDispatcher>();
		store.AddFeature<ComponentTestState>(new ComponentTestFeature());

		var cut1 = this.Render<SiblingSubscriberComponent>(p => p.Add(c => c.Label, "first"));
		var cut2 = this.Render<SiblingSubscriberComponent>(p => p.Add(c => c.Label, "second"));

		await cut1.InvokeAsync(() =>
			dispatcher.Dispatch(new ComponentTestActions.IncrementBy(3)));

		cut1.WaitForAssertion(() => Assert.Equal(3, cut1.Instance.LastObservedCounter));
		cut2.WaitForAssertion(() => Assert.Equal(3, cut2.Instance.LastObservedCounter));
	}
}
