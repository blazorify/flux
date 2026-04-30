using Blazorify.Flux.Core;
using Blazorify.Flux.Tests.Unit.Fixtures;
using Xunit;

namespace Blazorify.Flux.Tests.Unit;

public class StateTests {
	// === Get + ctor ===

	[Fact]
	public void Get_WhenDefault_ReturnsDefaultState() {
		var state = new State<TestState>();

		var result = state.Get();

		Assert.Equal(0, result.Counter);
		Assert.Equal(string.Empty, result.Name);
	}

	[Fact]
	public void Get_WhenInitializedWithValue_ReturnsCorrectState() {
		var state = new State<TestState>(new TestState { Counter = 5, Name = "init" });

		var result = state.Get();

		Assert.Equal(5, result.Counter);
		Assert.Equal("init", result.Name);
	}

	// === Set ===

	[Fact]
	public void Set_WhenCalled_UpdatesAllProperties() {
		var state = new State<TestState>();

		state.Set(new TestState { Counter = 3, Name = "x" });

		var result = state.Get();
		Assert.Equal(3, result.Counter);
		Assert.Equal("x", result.Name);
	}

	// === ApplyChanges ===

	[Fact]
	public void ApplyChanges_WhenPropertyChanged_ReturnsTrueAndUpdatedState() {
		var state = new State<TestState>();

		var changed = state.ApplyChanges(new TestState { Counter = 1 }, out var newState);

		Assert.True(changed);
		Assert.Equal(1, newState.Counter);
	}

	[Fact]
	public void ApplyChanges_WhenNoPropertyChanged_ReturnsFalse() {
		// Default state Counter=0, Name="". Apply identical state — no property differs.
		var state = new State<TestState>();

		var changed = state.ApplyChanges(new TestState { Counter = 0, Name = string.Empty }, out _);

		Assert.False(changed);
	}

	[Fact]
	public void ApplyChanges_WhenOnePropertyChanges_UpdatesOnlyThatProperty() {
		// Property-level change detection: only Counter changes; Name stays equal.
		var state = new State<TestState>(new TestState { Counter = 5, Name = "hello" });

		var changed = state.ApplyChanges(new TestState { Counter = 10, Name = "hello" }, out var newState);

		Assert.True(changed);
		Assert.Equal(10, newState.Counter);
		Assert.Equal("hello", newState.Name);
	}

	[Fact]
	public void ApplyChanges_WhenCalledRepeatedly_AccumulatesChanges() {
		var state = new State<TestState>();

		state.ApplyChanges(new TestState { Counter = 1 }, out _);
		state.ApplyChanges(new TestState { Counter = 2 }, out _);

		Assert.Equal(2, state.Get().Counter);
	}

	[Fact]
	public void Get_AfterApplyChanges_ReflectsLatestState() {
		// D-25a: assert value equality, NOT reference equality.
		var state = new State<TestState>();

		state.ApplyChanges(new TestState { Counter = 5 }, out _);

		Assert.Equal(5, state.Get().Counter);
	}

	// === Slice API (Get<TSlice> / Set<TSlice>) ===

	[Fact]
	public void GetSlice_WhenInvalidExpression_ThrowsInvalidOperationException() {
		// State.cs:49-51 — selector body must be a MemberExpression. A `new NestedSubState()`
		// expression body is a NewExpression, not a MemberExpression — triggers the throw.
		var state = new State<NestedState>();

		Assert.Throws<InvalidOperationException>(
			() => state.Get<NestedSubState>(_ => new NestedSubState())
		);
	}

	[Fact]
	public void SetSlice_WhenSelectorValid_UpdatesProperty() {
		var state = new State<NestedState>();

		state.Set<NestedSubState>(s => s.Sub, new NestedSubState { Value = 42 });

		Assert.Equal(42, state.Get<NestedSubState>(s => s.Sub).Value);
	}

	[Fact]
	public void GetSlice_WhenSliceValueIsNull_ThrowsInvalidOperationException() {
		// State.cs:53 — `value is not TSlice typedValue` triggers when the dictionary
		// holds null for the slice. NullableSubState.Sub is typed non-nullable but
		// initialized to `default!` so the dictionary entry is null at runtime.
		// The selector is therefore Func<NullableSubState, NestedSubState> cleanly,
		// no null-forgiving needed at the lambda site.
		var state = new State<NullableSubState>(new NullableSubState());

		Assert.Throws<InvalidOperationException>(
			() => state.Get<NestedSubState>(s => s.Sub)
		);
	}

	// === Immutability ===

	[Fact]
	public void Immutability_AfterApplyChangesAndGet_ReflectsLatestValue() {
		// Records with init-only properties are immutable. Verify the State<TState>
		// internal ImmutableDictionary was updated by ApplyChanges, not just a returned
		// reference. Caller cannot mutate state externally and affect future Get() calls.
		var state = new State<TestState>();
		var firstGet = state.Get();
		Assert.Equal(0, firstGet.Counter);   // baseline

		state.ApplyChanges(new TestState { Counter = 99 }, out _);

		// Second Get reflects the new value — proves the dictionary was updated.
		Assert.Equal(99, state.Get().Counter);
		Assert.Equal(0, firstGet.Counter);   // firstGet unchanged (record immutability)
	}

	// === Inline fixtures (private nested) ===

	private record NestedSubState {
		public int Value { get; init; }
	}

	private record NestedState {
		public NestedSubState Sub { get; init; } = new();
	}

	private record NullableSubState {
		// Type-wise non-nullable so `Get<NestedSubState>(s => s.Sub)` infers
		// Func<NullableSubState, NestedSubState> without null-forgiving on the
		// lambda body. Runtime value is null via `default!` so the State<T>
		// dictionary stores null for "Sub" — exercises the State.cs:53 null path.
		public NestedSubState Sub { get; init; } = default!;
	}
}
