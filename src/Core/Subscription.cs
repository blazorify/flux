using System;

namespace Blazorify.Flux.Core {
	public class Subscription : IDisposable {
		private readonly Action unsubscribe;
		private Boolean disposed;

		public Subscription(Action unsubscribe) {
			this.unsubscribe = unsubscribe;
		}

		public void Dispose() {
			if (this.disposed) {
				return;
			}

			this.disposed = true;
			this.unsubscribe();
		}
	}
}
