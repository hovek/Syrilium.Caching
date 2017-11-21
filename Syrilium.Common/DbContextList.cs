using Syrilium.CommonInterface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syrilium.Common
{
	public class DbContextList<T> : TSList<T>
	{
		public DbContext DbContext { get; set; }
		public DbSet DbSet { get; set; }
		public bool ReflectChangesToDbSet = true;
		public Func<ListChangedType, T, object> DbSetObjectNeeded;

		public DbContextList(int? capacity = null, DbContext dbContext = null)
			: base(capacity)
		{
			SetDbContext(dbContext);
			init();
		}

		public DbContextList(IEnumerable<T> collection, DbContext dbContext = null)
			: this(collection, null, dbContext)
		{
		}

		public DbContextList(IEnumerable<T> collection, ListChangedEventHandler listChangedEventHandler, DbContext dbContext = null)
			: base(collection, listChangedEventHandler)
		{
			SetDbContext(dbContext);
			init();
		}

		private void init()
		{
			ListChanged += DbContextList_ListChanged;
		}

		new public DbContextList<T> SetAddNew<TNew>()
		{
			return (DbContextList<T>)base.SetAddNew<TNew>();
		}

		public DbContextList<T> SetDbContext(DbContext dbContext)
		{
			DbContext = dbContext;
			var tt = typeof(T);
			if (!tt.IsInterface)
			{
				SetDbSet<T>();
			}
			return this;
		}

		public DbContextList<T> SetDbSet<TNew>()
		{
			DbSet = DbContext?.Set(typeof(TNew));
			return this;
		}

		private void DbContextList_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (!ReflectChangesToDbSet) return;

			var changeInfo = (TSList<T>.ChangeInfo)sender;

			if (changeInfo.ExtraInfo?.ToString() != "ByFilter")
			{
				foreach (var i in changeInfo.AddedItems)
					DbSet.Add(DbSetObjectNeeded?.Invoke(ListChangedType.ItemAdded, (T)i) ?? i);

				foreach (var i in changeInfo.RemovedItems)
					DbSet.Remove(DbSetObjectNeeded?.Invoke(ListChangedType.ItemDeleted, (T)i) ?? i);
			}
		}

		public void SaveChanges()
		{
			DbContext.SaveChanges();

			var wasAddNew = addNewItems.ReadWrite(addNewItems =>
			{
				if (addNewItems.Value.Count > 0)
				{
					addNewItems.Write(_ => addNewItems.Value.Clear());
					return true;
				}
				return false;
			});

			if (wasAddNew && IsSorted)
				applySort(sortProperty, sortDirection);
		}

		public void Rollback()
		{
			var changedEntries = DbContext.ChangeTracker.Entries()
					.Where(x => x.State == EntityState.Added || x.State == EntityState.Deleted || x.State == EntityState.Modified);
			foreach (var e in changedEntries)
				Rollback(e);
		}

		public void Rollback(object entity)
		{
			Rollback(DbContext.Entry(entity));
		}

		public void Rollback(DbEntityEntry entry)
		{
			switch (entry.State)
			{
				case EntityState.Added:
					Remove(entry.Entity);
					break;
				case EntityState.Deleted:
					Add(entry.Entity);
					break;
				case EntityState.Modified:
					((IDbContextRollback)DbContext).Rollback(entry.Entity);
					break;
			}
		}
	}
}
