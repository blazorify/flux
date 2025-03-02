using System;
using Blazorify.Flux.Interfaces;

namespace Blazorify.Flux.Demo.State {
	public class CounterActions {
		public record Increment : IAction {
		}

		public record Decrement : IAction {
		}
	}
}
