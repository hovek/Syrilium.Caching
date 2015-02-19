using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Syrilium.CommonInterface
{
	public class EnumStringValue : Attribute
	{
		public string Value = null;

		public EnumStringValue(string value)
		{
			Value = value;
		}

		public static T? GetEnumValue<T>(string value) where T : struct
		{
			Type type = typeof(T);

			FieldInfo[] fieldInfos = type.GetFields();

			foreach (FieldInfo fieldInfo in fieldInfos)
			{
				EnumStringValue[] stringValues = (EnumStringValue[])fieldInfo.GetCustomAttributes(typeof(EnumStringValue), false);

				foreach (EnumStringValue stringValue in stringValues)
				{
					if (stringValue.Value == value)
					{
						return (T)fieldInfo.GetRawConstantValue();
					}
				}
			}

			return null;
		}

		public static string GetStringValue<T>(T value) where T : struct
		{
			Type type = typeof(T);

			FieldInfo[] fieldInfos = type.GetFields();

			foreach (FieldInfo fieldInfo in fieldInfos)
			{
				if (fieldInfo.IsLiteral && value.Equals((T)fieldInfo.GetRawConstantValue()))
				{
					EnumStringValue[] stringValues = (EnumStringValue[])fieldInfo.GetCustomAttributes(typeof(EnumStringValue), false);
					if (stringValues.Length > 0)
					{
						return stringValues[0].Value;
					}
				}
			}

			return null;
		}
	}
}
