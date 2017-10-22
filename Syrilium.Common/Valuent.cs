using System;
using System.Collections.Generic;
using System.Text;

namespace Syrilium.Common
{
	/// <summary>
	/// Valuent zamotava vrijednost, predstavlja ju te daje mogućnost postavljanja da li vrijednost postoji, ili ne, bez obzira na vrijednost koju sadrži.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	[Serializable]
	public class Valuent<T>
	{
		public Valuent()
		{
			_value = default(T);
		}

		public Valuent(T value)
		{
			Value = value;
		}

		public Valuent(T value, bool hasValue)
		{
			_value = value;
			HasValue = hasValue;
		}

		private T _value;
		public T Value
		{
			get
			{
				return _value;
			}
			set
			{
				_value = value;
				HasValue = true;
			}
		}

		private bool _hasValue;
		public bool HasValue
		{
			get
			{
				return _hasValue;
			}
			set
			{
				_hasValue = value;
			}
		}

		public static implicit operator T(Valuent<T> valuent)
		{
			return valuent == null ? default(T) : valuent.Value;
		}

		public static implicit operator Valuent<T>(T value)
		{
			return new Valuent<T>(value);
		}
	}
}
