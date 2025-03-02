# Blazorify.Flux

Blazorify.Flux is a Flux architecture implementation for Blazor, providing a structured and predictable approach to state management using unidirectional data flow.

## Installation

Install the package via NuGet:

```bash
dotnet add package Blazorify.Flux
```

## Usage

### 1. Register the Store
Add `Blazorify.Flux` services in `Program.cs`:

```csharp
builder.Services.AddBlazorifyFlux();
```

### 2. Define Actions
Actions represent state changes in the application:

```csharp
public static class CounterActions {
	public record Increment : IAction;
	public record Decrement : IAction;
}
```

### 3. Create a Feature
A feature manages a specific state slice and defines how it responds to actions.

```csharp
public class CounterFeature : FeatureBase<CounterState> {
	public CounterFeature(IStore store) : base(store) { }

	protected override void ConfigureReducers(ReducerBuilder builder) {
		builder.On<CounterActions.Increment>((state, action) => state with { CurrentCount = state.CurrentCount + 1 });
		builder.On<CounterActions.Decrement>((state, action) => state with { CurrentCount = state.CurrentCount - 1 });
	}

	protected override void ConfigureEffects(EffectBuilder builder) {
		// No effects needed
	}
}
```

### 4. Initialize the Store
Add the store initializer in `App.razor`:

```razor
<Blazorify.Flux.Components.StoreInitializer />

<Router AppAssembly="@typeof(App).Assembly">
   ...
</Router>
```

### 5. Use the Store in a Component
Inject `IStore` and subscribe to state changes.

```razor
@implements IDisposable
@inject Blazorify.Flux.Interfaces.IStore store

<p role="status">Current count: @currentCount</p>

<button class="btn btn-primary" @onclick="IncrementCount">Increment</button>
<button class="btn btn-primary" @onclick="DecrementCount">Decrement</button>

@code {
	private int currentCount = 0;
	private IDisposable? subscription;

	protected override void OnInitialized() {
		base.OnInitialized();
		this.subscription = this.store.Subscribe<CounterState>(state => this.currentCount = state.CurrentCount);
	}

	private void IncrementCount() => this.store.Dispatch<CounterActions.Increment>();
	private void DecrementCount() => this.store.Dispatch<CounterActions.Decrement>();

	public void Dispose() => this.subscription?.Dispose();
}
```

### 6. Use `FluxComponent` for Simplicity
Alternatively, inherit from `FluxComponent` for automatic subscription management.

```razor
@inherits Blazorify.Flux.Components.FluxComponent

<p role="status">Current count: @currentCount</p>

<button class="btn btn-primary" @onclick="IncrementCount">Increment</button>
<button class="btn btn-primary" @onclick="DecrementCount">Decrement</button>

@code {
	private int currentCount = 0;

	protected override void OnInitialized() {
		base.OnInitialized();
		this.Subscribe<CounterState>(state => this.currentCount = state.CurrentCount);
	}

	private void IncrementCount() => this.Dispatch<CounterActions.Increment>();
	private void DecrementCount() => this.Dispatch<CounterActions.Decrement>();
}
```

---

## Documentation

More details coming soon.

## Prerequisites

## Contributing
