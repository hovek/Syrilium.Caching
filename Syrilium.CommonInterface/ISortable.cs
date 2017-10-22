using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syrilium.CommonInterface
{
	public interface ISortable
	{
		void Sort();
		void Sort(IComparer comparer);
		void Sort(int index, int count, IComparer comparer);
	}
}
