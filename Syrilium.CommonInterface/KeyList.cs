using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Syrilium.CommonInterface
{
	public interface IKeyList<TKey, T> : IList<T>
	{
		T this[TKey key] { get; set; }
	}

	public class KeyList<TKey, T> : List<T>, IKeyList<TKey, T>
	{
		public event OneParamReturnDelegate<T, TKey> GetItem;
		public event TwoParamDelegate<TKey, T> SetItem;

		public T this[TKey key]
		{
			get
			{
				if (GetItem != null)
				{
					return GetItem(key);
				}

				return default(T);
			}
			set
			{
				if (SetItem != null)
				{
					SetItem(key, value);
				}

				throw new NotImplementedException();
			}
		}
	}
}
