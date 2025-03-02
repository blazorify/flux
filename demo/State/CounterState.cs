using System;

namespace Blazorify.Flux.Demo.State {
	public record CounterState {
		public Int32 CurrentCount { get; set; } = 0;
	}
}
