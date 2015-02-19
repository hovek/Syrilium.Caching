using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Syrilium.CommonInterface
{
    public class ObservableCollection<T> : System.Collections.ObjectModel.ObservableCollection<T>
    {
        public T Find(Predicate<T> match)
        {
            foreach (T item in this)
            {
                if (match(item))
                    return item;
            }

            return default(T);
        }

        public bool Exists(Predicate<T> match)
        {
            foreach (T item in this)
            {
                if (match(item))
                    return true;
            }

            return false;
        }

        public void ForEach(Action<T> action)
        {
            foreach (T item in this)
            {
                action(item);
            }
        }

        //
        // Summary:
        //     Converts the elements in the current System.Collections.Generic.List<T> to
        //     another type, and returns a list containing the converted elements.
        //
        // Parameters:
        //   converter:
        //     A System.Converter<TInput,TOutput> delegate that converts each element from
        //     one type to another type.
        //
        // Type parameters:
        //   TOutput:
        //     The type of the elements of the target array.
        //
        // Returns:
        //     A System.Collections.Generic.List<T> of the target type containing the converted
        //     elements from the current System.Collections.Generic.List<T>.
        //
        // Exceptions:
        //   System.ArgumentNullException:
        //     converter is null.
        public List<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
        {
            List<TOutput> result = new List<TOutput>();
            foreach (T i in (ObservableCollection<T>)converter.Target)
            {
                result.Add(converter(i));
            }

            return result;
        }

        //
        // Summary:
        //     Adds the elements of the specified collection to the end of the System.Collections.Generic.List<T>.
        //
        // Parameters:
        //   collection:
        //     The collection whose elements should be added to the end of the System.Collections.Generic.List<T>.
        //     The collection itself cannot be null, but it can contain elements that are
        //     null, if type T is a reference type.
        //
        // Exceptions:
        //   System.ArgumentNullException:
        //     collection is null.
        public void AddRange(IEnumerable<T> collection)
        {
            foreach (T i in collection)
            {
                this.Add(i);
            }
        }
    }

	public static class ObservableCollectionExtensions
	{
		public static void RemoveAll(this IList list)
		{
			while (list.Count > 0)
			{
				list.RemoveAt(0);
			}
		}
	}
}
