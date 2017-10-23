using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Syrilium.CommonInterface
{
	public static class ExtensionMethods
	{
		#region String
		public static decimal? ToDecimalNullable(this string decStr)
		{
			decimal dec;
			if (decimal.TryParse(decStr, out dec))
				return dec;
			return null;
		}
		public static decimal ToDecimal(this string decStr)
		{
			decimal dec;
			if (decimal.TryParse(decStr, out dec))
				return dec;
			return 0;
		}
		public static bool IsNullOrEmpty(this string source)
		{
			return source == null || source == string.Empty;
		}
		public static int ToInt(this string intStr)
		{
			int int32;
			if (int.TryParse(intStr, out int32))
				return int32;
			return 0;
		}

		public static string GetRows(this string str, int maxRows)
		{
			if (str == null) return str;

			var sb = new StringBuilder();

			string newLine = "\n";
			int prevRowIndex = 0;
			int rowIndex;
			int row = 0;
			while ((rowIndex = str.IndexOf(newLine, prevRowIndex)) != -1 && row < maxRows)
			{
				int rowIdxAndRow = rowIndex + newLine.Length;
				sb.Append(str.Substring(prevRowIndex, rowIdxAndRow - prevRowIndex));
				prevRowIndex = rowIdxAndRow;
				row++;
			}

			if (rowIndex == -1 && row < maxRows)
				sb.Append(str.Substring(prevRowIndex, str.Length - prevRowIndex));

			return sb.ToString();
		}

		public static T Deserialize<T>(this string xml)
		{
			var ms = new MemoryStream(Encoding.Unicode.GetBytes(xml));
			return (T)new XmlSerializer(typeof(T)).Deserialize(ms);
		}
		#endregion

		#region Object
		/// <summary>
		/// Even ((object)null).To_String() won't throw exception.
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="nullIsEmpty"></param>
		/// <returns></returns>
		public static string ToStringEx(this object obj, bool nullIsEmpty = false)
		{
			return obj == null ? (nullIsEmpty ? "" : null) : obj.ToString();
		}

		public static Nullable<T> AsNullable<T>(this T value)
			where T : struct
		{
			return (T?)value;
		}

		#region IfNotNull
		public static TResult IfNotNull<TA, TResult>(this TA arg, Func<TA, TResult> expression1, TResult nullValue = default(TResult))
		{
			return arg == null ? nullValue : expression1(arg);
		}

		public static TResult IfNotNull<TArg, TRA, TResult>(this TArg arg, Func<TArg, TRA> expression1, Func<TRA, TResult> expression2, TResult nullValue = default(TResult))
		{
			if (arg == null) return nullValue;
			var tra = expression1(arg);
			if (tra == null) return nullValue;
			return expression2(tra);
		}
		public static TResult IfNotNull<TArg, TRA, TRB, TResult>(this TArg arg, Func<TArg, TRA> expression1, Func<TRA, TRB> expression2, Func<TRB, TResult> expression3, TResult nullValue = default(TResult))
		{
			if (arg == null) return nullValue;
			var tra = expression1(arg);
			if (tra == null) return nullValue;
			var trb = expression2(tra);
			if (trb == null) return nullValue;
			return expression3(trb);
		}
		public static TResult IfNotNull<TArg, TRA, TRB, TRC, TResult>(this TArg arg, Func<TArg, TRA> expression1, Func<TRA, TRB> expression2,
			Func<TRB, TRC> expression3, Func<TRC, TResult> expression4, TResult nullValue = default(TResult))
		{
			if (arg == null) return nullValue;
			var tra = expression1(arg);
			if (tra == null) return nullValue;
			var trb = expression2(tra);
			if (trb == null) return nullValue;
			var trc = expression3(trb);
			if (trc == null) return nullValue;
			return expression4(trc);
		}
		public static TResult IfNotNull<TArg, TRA, TRB, TRC, TRD, TResult>(this TArg arg, Func<TArg, TRA> expression1, Func<TRA, TRB> expression2,
			Func<TRB, TRC> expression3, Func<TRC, TRD> expression4, Func<TRD, TResult> expression5, TResult nullValue = default(TResult))
		{
			if (arg == null) return nullValue;
			var tra = expression1(arg);
			if (tra == null) return nullValue;
			var trb = expression2(tra);
			if (trb == null) return nullValue;
			var trc = expression3(trb);
			if (trc == null) return nullValue;
			var trd = expression4(trc);
			if (trd == null) return nullValue;
			return expression5(trd);
		}
		public static TResult IfNotNull<TArg, TRA, TRB, TRC, TRD, TRE, TResult>(this TArg arg, Func<TArg, TRA> expression1, Func<TRA, TRB> expression2,
			Func<TRB, TRC> expression3, Func<TRC, TRD> expression4, Func<TRD, TRE> expression5, Func<TRE, TResult> expression6, TResult nullValue = default(TResult))
		{
			if (arg == null) return nullValue;
			var tra = expression1(arg);
			if (tra == null) return nullValue;
			var trb = expression2(tra);
			if (trb == null) return nullValue;
			var trc = expression3(trb);
			if (trc == null) return nullValue;
			var trd = expression4(trc);
			if (trd == null) return nullValue;
			var tre = expression5(trd);
			if (tre == null) return nullValue;
			return expression6(tre);
		}
		#endregion

		public static string Serialize<T>(this T value)
		{
			if (value == null)
			{
				return null;
			}

			XmlSerializer xmlserializer = new XmlSerializer(typeof(T));
			StringWriter stringWriter = new StringWriter();
			XmlWriter writer = XmlWriter.Create(stringWriter);

			xmlserializer.Serialize(writer, value);

			var serializeXml = stringWriter.ToString();

			writer.Close();
			return serializeXml;
		}

		public static Wrap<T> Wrap<T>(this T obj, ICollection<IWrap> wrapperCollection = null)
		{
			var wrapper = new Wrap<T>(obj);
			if (wrapperCollection != null) wrapperCollection.Add(wrapper);
			return wrapper;
		}
		#endregion

		#region Expression
		public static MethodInfo ExtractMethodObjects(this LambdaExpression mtd, out object instance, out object[] parameters)
		{
			if (!(mtd.Body is MethodCallExpression))
				throw new InvalidOperationException("LambdaExpression must contain method.");

			var methodCallExpression = mtd.Body as MethodCallExpression;

			instance = methodCallExpression.Object.GetObject();

			parameters = new object[methodCallExpression.Arguments.Count];
			for (int i = 0; i < methodCallExpression.Arguments.Count; i++)
				parameters[i] = methodCallExpression.Arguments[i].GetObject();

			return methodCallExpression.Method;
		}

		public static object GetObject(this Expression exp)
		{
			if (exp is ConstantExpression)
				return ((ConstantExpression)exp).Value;
			else if (exp is MemberExpression)
			{
				var memberExpression = (MemberExpression)exp;
				var obj = ((ConstantExpression)memberExpression.Expression).Value;
				return ((FieldInfo)memberExpression.Member).GetValue(obj);
			}
			else
				return ((ParameterExpression)exp).Type;
		}
		#endregion
	}
}
