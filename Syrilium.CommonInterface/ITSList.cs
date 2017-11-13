using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syrilium.CommonInterface
{
	public interface ITSList<T> : IList<T>, IBindingListView, IRaiseItemChangedEvents, ICloneable, ISortable, ICancelAddNew
	{
		new T this[int index]
		{
			get;
			set;
		}

		event EventHandler OnEndNew;
		void ApplyFilter(Predicate<T> filter);
	}
}
