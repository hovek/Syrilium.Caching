using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syrilium.CommonInterface
{
	public interface IWrap
	{
		/// <summary>
		/// Wrapped object.
		/// </summary>
		object Object { get; set; }
	}

	public interface IWrap<T>
	{
		/// <summary>
		/// Wrapped object.
		/// </summary>
		T _ { get; set; }
	}

	public class Wrap<T> : IWrap<T>, IWrap
	{
		private T value;
		/// <summary>
		/// Wrapped object.
		/// </summary>
		public T _
		{
			get { return value; }
			set { this.value = value; }
		}

		/// <summary>
		/// Wrapped object.
		/// </summary>
		public object Object
		{
			get { return value; }
			set { this.value = (T)value; }
		}

		public Wrap(T value)
		{
			this.value = value;
		}

		public static implicit operator T(Wrap<T> w)  
		{
			return w.value;
		}

		public override int GetHashCode()
		{
			return value == null ? 0 : value.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return value.Equals(obj is IWrap ? ((IWrap)obj).Object : obj);
		}
	}
}
