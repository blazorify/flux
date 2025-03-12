using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Blazorify.Flux.Entities {
	public class EntityState<TEntity, TKey> where TEntity : class, new() where TKey : notnull {
		private ImmutableDictionary<TKey, TEntity> entities = ImmutableDictionary<TKey, TEntity>.Empty;
		private ImmutableHashSet<TKey> keys = ImmutableHashSet<TKey>.Empty;

		private readonly Func<TEntity, TKey> keySelector;

		public EntityState(
			IEnumerable<TEntity> initialEntities,
			Func<TEntity, TKey> keySelector
		) {
			this.keySelector = keySelector;

			this.SetAll(initialEntities);
		}
		public EntityState<TEntity, TKey> SetOne(TEntity entity) {
			var key = this.keySelector(entity);

			this.entities = this.entities.SetItem(key, entity);

			return this;
		}

		public EntityState<TEntity, TKey> SetMany(IEnumerable<TEntity> entities) {
			var updatedEntities = entities.ToImmutableDictionary(this.keySelector);

			this.entities = this.entities.SetItems(updatedEntities);

			return this;
		}

		public EntityState<TEntity, TKey> SetAll(IEnumerable<TEntity> entities) {
			this.entities = entities.ToImmutableDictionary(this.keySelector);
			this.keys = entities.Select(this.keySelector).ToImmutableHashSet();

			return this;
		}

		public EntityState<TEntity, TKey> AddOne(TEntity entity) {
			var key = this.keySelector(entity);

			if (this.keys.Contains(key)) {
				return this;
			}

			this.entities = this.entities.Add(key, entity);
			this.keys = this.keys.Add(key);

			return this;
		}

		public EntityState<TEntity, TKey> AddMany(IEnumerable<TEntity> entities) {
			var newEntities = entities
				.Where(entity => !this.keys.Contains(this.keySelector(entity)))
				.ToImmutableDictionary(this.keySelector);

			var newKeys = newEntities.Keys.ToImmutableHashSet();

			this.entities = this.entities.AddRange(newEntities);
			this.keys = this.keys.Union(newKeys);

			return this;
		}

		public EntityState<TEntity, TKey> UpdateOne(TEntity entity) {
			var key = this.keySelector(entity);

			if (!this.entities.ContainsKey(key)) {
				return this;
			}

			this.entities = this.entities.SetItem(key, entity);

			return this;
		}

		public EntityState<TEntity, TKey> UpdateMany(IEnumerable<TEntity> entities) {
			var updatedEntities = entities
				.Where(entity => this.keys.Contains(this.keySelector(entity)))
				.ToImmutableDictionary(this.keySelector);

			this.entities = this.entities.SetItems(updatedEntities);

			return this;
		}

		public EntityState<TEntity, TKey> UpsertOne(TEntity entity) {
			return this.keys.Contains(this.keySelector(entity))
				? this.UpdateOne(entity)
				: this.AddOne(entity);
		}

		public EntityState<TEntity, TKey> UpsertMany(IEnumerable<TEntity> entities) {
			var entitiesToAdd = entities.Where(entity => !this.keys.Contains(this.keySelector(entity)));

			if (entitiesToAdd.Any()) {
				this.AddMany(entitiesToAdd);
			}

			var entitiesToUpdate = entities.Where(entity => this.keys.Contains(this.keySelector(entity)));

			if (entitiesToUpdate.Any()) {
				this.UpdateMany(entitiesToUpdate);
			}

			return this;
		}

		public EntityState<TEntity, TKey> RemoveOne(TEntity entity) {
			return this.RemoveOne(this.keySelector(entity));
		}

		public EntityState<TEntity, TKey> RemoveOne(TKey key) {
			if (!this.keys.Contains(key)) {
				return this;
			}

			this.entities = this.entities.Remove(key);
			this.keys = this.keys.Remove(key);

			return this;
		}

		public EntityState<TEntity, TKey> RemoveMany(IEnumerable<TEntity> entities) {
			return this.RemoveMany(entities.Select(this.keySelector));
		}

		public EntityState<TEntity, TKey> RemoveMany(IEnumerable<TKey> keys) {
			this.entities = this.entities.RemoveRange(keys);
			this.keys = this.keys.Except(keys);

			return this;
		}

		public EntityState<TEntity, TKey> RemoveAll() {
			this.entities = ImmutableDictionary<TKey, TEntity>.Empty;
			this.keys = ImmutableHashSet<TKey>.Empty;

			return this;
		}
	}
}
