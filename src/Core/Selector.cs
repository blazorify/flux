using System;
using System.Collections.Generic;
using System.Threading;
using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Core {
	internal sealed class MemoizedSelector<TState, TResult> : ISelector<TState, TResult>
		where TState : class, new() {

		private readonly Lock syncRoot = new();
		private readonly Func<TState, TResult> projector;
		private readonly IEqualityComparer<TState> inputComparer;
		private readonly IEqualityComparer<TResult> outputComparer;

		private TState? lastInput;
		private TResult lastOutput = default!;
		private Boolean hasResult;

		public MemoizedSelector(
			Func<TState, TResult> projector,
			IEqualityComparer<TState> inputComparer,
			IEqualityComparer<TResult> outputComparer
		) {
			this.projector = projector;
			this.inputComparer = inputComparer;
			this.outputComparer = outputComparer;
		}

		public TResult LastResult {
			get {
				lock (this.syncRoot) {
					if (!this.hasResult) {
						throw new InvalidOperationException("No projection has been computed yet. Call Select(state) first.");
					}
					return this.lastOutput;
				}
			}
		}

		public TResult Select(TState state) {
			// Projector runs inside the lock so concurrent calls don't lose updates.
			// Safe because projectors are pure and synchronous per Flux contract.
			lock (this.syncRoot) {
				if (this.hasResult && this.inputComparer.Equals(state, this.lastInput!)) {
					return this.lastOutput;
				}

				var newOutput = this.projector(state);

				if (this.hasResult && this.outputComparer.Equals(newOutput, this.lastOutput)) {
					this.lastInput = state;
					// Preserve the cached reference so callers comparing by reference can detect no-change.
					return this.lastOutput;
				}

				this.lastInput = state;
				this.lastOutput = newOutput;
				this.hasResult = true;
				return newOutput;
			}
		}

		public void Reset() {
			lock (this.syncRoot) {
				this.lastInput = null;
				this.lastOutput = default!;
				this.hasResult = false;
			}
		}
	}

	internal sealed class ComposedSelector<TState1, TState2, TInput1, TInput2, TResult>
		: IComposedSelector<TResult>
		where TState1 : class, new()
		where TState2 : class, new() {

		private readonly Lock syncRoot = new();
		private readonly IStore store;
		private readonly ISelector<TState1, TInput1> selectorA;
		private readonly ISelector<TState2, TInput2> selectorB;
		private readonly Func<TInput1, TInput2, TResult> combiner;

		private TInput1 lastA = default!;
		private TInput2 lastB = default!;
		private TResult lastResult = default!;
		private Boolean hasResult;

		public ComposedSelector(
			IStore store,
			ISelector<TState1, TInput1> selectorA,
			ISelector<TState2, TInput2> selectorB,
			Func<TInput1, TInput2, TResult> combiner
		) {
			this.store = store;
			this.selectorA = selectorA;
			this.selectorB = selectorB;
			this.combiner = combiner;
		}

		public TResult LastResult {
			get {
				lock (this.syncRoot) {
					if (!this.hasResult) {
						throw new InvalidOperationException("No projection has been computed yet. Call Select() first.");
					}
					return this.lastResult;
				}
			}
		}

		public TResult Select() {
			var feat1 = this.store.GetFeature<TState1>()
				?? throw new InvalidOperationException(
					$"Composed selector cannot project: feature for state '{typeof(TState1).FullName}' is not registered. Call Store.Initialize() and ensure a Feature<{typeof(TState1).Name}> is discoverable.");
			var feat2 = this.store.GetFeature<TState2>()
				?? throw new InvalidOperationException(
					$"Composed selector cannot project: feature for state '{typeof(TState2).FullName}' is not registered. Call Store.Initialize() and ensure a Feature<{typeof(TState2).Name}> is discoverable.");

			var inputA = this.selectorA.Select(feat1.State);
			var inputB = this.selectorB.Select(feat2.State);

			// Combiner runs inside the lock so concurrent Select() calls don't lose updates.
			// Safe because combiners are pure and synchronous per Flux contract.
			lock (this.syncRoot) {
				if (this.hasResult
					&& EqualityComparer<TInput1>.Default.Equals(inputA, this.lastA)
					&& EqualityComparer<TInput2>.Default.Equals(inputB, this.lastB)) {
					return this.lastResult;
				}

				this.lastA = inputA;
				this.lastB = inputB;
				this.lastResult = this.combiner(inputA, inputB);
				this.hasResult = true;
				return this.lastResult;
			}
		}

		public void Reset() {
			lock (this.syncRoot) {
				this.lastA = default!;
				this.lastB = default!;
				this.lastResult = default!;
				this.hasResult = false;
			}
		}
	}
}
