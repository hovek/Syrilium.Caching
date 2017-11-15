using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syrilium.CommonInterface
{
	public interface IDbContextRollback
	{
		void Rollback(object entity);
		void Rollback<TEntity>(TEntity entity) where TEntity : class;
		void Rollback();
		void Rollback(DbEntityEntry entry);
	}
}
