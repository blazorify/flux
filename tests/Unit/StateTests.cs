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
		var state = new State<TestState>();

		var changed = state.ApplyChanges(new TestState { Counter = 0, Name = string.Empty }, out _);

		Assert.False(changed);
	}

	[Fact]
	public void ApplyChanges_WhenOnePropertyChanges_UpdatesOnlyThatProperty() {
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
		var state = new State<TestState>();

		state.ApplyChanges(new TestState { Counter = 5 }, out _);

		Assert.Equal(5, state.Get().Counter);
	}

	// === Slice API (Get<TSlice> / Set<TSlice>) ===

	[Fact]
	public void GetSlice_WhenInvalidExpression_ThrowsInvalidOperationException() {
		// Selector body must be a MemberExpression; `new NestedSubState()` is a
		// NewExpression and triggers the throw.
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
		// Get<> throws when the underlying property is null at runtime
		// (NullableSubState.Sub is non-nullable but initialized to default!).
		var state = new State<NullableSubState>(new NullableSubState());

		Assert.Throws<InvalidOperationException>(
			() => state.Get<NestedSubState>(s => s.Sub)
		);
	}

	// === Immutability ===

	[Fact]
	public void Immutability_AfterApplyChangesAndGet_ReflectsLatestValue() {
		// Verify ApplyChanges swaps internal state (Get returns 99) without mutating
		// references already held by callers (firstGet stays at 0).
		var state = new State<TestState>();
		var firstGet = state.Get();
		Assert.Equal(0, firstGet.Counter);

		state.ApplyChanges(new TestState { Counter = 99 }, out _);

		Assert.Equal(99, state.Get().Counter);
		Assert.Equal(0, firstGet.Counter);
	}

	[Fact]
	public void Get_CalledTwice_ReturnsSameReference() {
		// Get() returns the cached reference between dispatches; no per-call allocation.
		var state = new State<TestState>();

		var first = state.Get();
		var second = state.Get();

		Assert.Same(first, second);
	}

	[Fact]
	public void Get_AfterApplyChanges_ReturnsNewReference() {
		// ApplyChanges must rebuild the cached reference; the prior one stays valid (record immutability).
		var state = new State<TestState>();
		var first = state.Get();

		state.ApplyChanges(new TestState { Counter = 1 }, out _);
		var second = state.Get();

		Assert.NotSame(first, second);
		Assert.Equal(1, second.Counter);
		Assert.Equal(0, first.Counter);
	}

	[Fact]
	public void ApplyChanges_UnderParallelFor_NoUpdatesLost() {
		// ApplyChanges' read-modify-write must be atomic — without syncRoot, two
		// threads racing foreach + SetItem corrupt state or throw.
		var state = new State<TestState>();

		var ex = Record.Exception(() =>
			Parallel.For(0, 100, i =>
				state.ApplyChanges(new TestState { Counter = i }, out _)
			)
		);

		Assert.Null(ex);
		Assert.InRange(state.Get().Counter, 0, 99);
	}

	// === Inline fixtures (private nested) ===

	private record NestedSubState {
		public int Value { get; init; }
	}

	private record NestedState {
		public NestedSubState Sub { get; init; } = new();
	}

	private record NullableSubState {
		// Declared non-nullable for clean inference of `Get<NestedSubState>(s => s.Sub)`;
		// `default!` makes it null at runtime to exercise State's null-property path.
		public NestedSubState Sub { get; init; } = default!;
	}
}
