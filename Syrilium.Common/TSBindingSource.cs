using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Syrilium.Common
{
	public class TSBindingSource : BindingSource
	{
		private object prevCurrent;

		public override object AddNew()
		{
			prevCurrent = ((IBindingList)DataSource).AddNew();
			repositionToCurrent();
			return prevCurrent;
		}

		protected override void OnPositionChanged(EventArgs e)
		{
			if (Position != -1)
				prevCurrent = Current;

			base.OnPositionChanged(e);
		}

		protected override void OnListChanged(ListChangedEventArgs e)
		{
			base.OnListChanged(e);
			repositionToCurrent();
		}

		private void repositionToCurrent()
		{
			if (Position != -1 && Current != prevCurrent)
			{
				for (int i = 0; i < this.List.Count; i++)
				{
					if (List[i].Equals(prevCurrent))
					{
						this.Position = i;
						break;
					}
				}
			}
		}
	}
}
