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
}
