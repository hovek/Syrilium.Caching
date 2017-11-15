using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Syrilium.Common
{
	public class NotifyBase : INotifyPropertyChanged, INotifyPropertyChanging
	{
		public void SetWithNotify<T>(T val, ref T field, [CallerMemberName] string prop = "")
		{
			if ((field == null && val != null) || !field.Equals(val))
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(prop));
				field = val;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		public event PropertyChangingEventHandler PropertyChanging;
	}
}
