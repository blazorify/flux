using System;

namespace Blazorify.Flux.Entities {
	public class EntityAdapter<TEntity, TKey> where TEntity : class, new() where TKey : notnull {
		private EntityState<TEntity, TKey> state;

		public EntityAdapter(EntityState<TEntity, TKey> initialState) {
			this.state = initialState;
		}
	}
}
