using System;

namespace Blazorify.Flux.Interfaces {
	public interface IDispatcher {
		void Dispatch<TAction>() where TAction : IAction, new();

		void Dispatch<TAction>(TAction action) where TAction : IAction;

		void Dispatch<TAction>(Func<TAction> action) where TAction : IAction;

		void Dispatch<TAction>(Func<TAction, TAction> action) where TAction : IAction, new();

		void Dispatch(IAction action);
	}
}
