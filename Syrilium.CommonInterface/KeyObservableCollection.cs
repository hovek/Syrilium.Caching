using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Syrilium.CommonInterface
{
	public class KeyObservableCollection<TKey, T> : ObservableCollection<T>, IKeyList<TKey, T>
	{
		public event OneParamReturnDelegate<T, TKey> Get;
		public event TwoParamDelegate<TKey, T> Set;

		public T this[TKey key]
		{
			get
			{
				if (Get != null)
				{
					return Get(key);
				}

				return default(T);
			}
			set
			{
				if (Set != null)
				{
					Set(key, value);
				}

				throw new NotImplementedException();
			}
		}
	}
}
