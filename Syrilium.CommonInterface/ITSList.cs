using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syrilium.CommonInterface
{
	public interface ITSList<T> : IList<T>, ICollection<T>, IEnumerable<T>, IBindingListView, IRaiseItemChangedEvents, ICloneable, ISortable
	{
	}
}
