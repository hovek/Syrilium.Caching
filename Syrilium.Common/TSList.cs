using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Collections;
using System.Reflection;
using Syrilium.CommonInterface;

namespace Syrilium.Common
{
	public delegate bool ReturnAction<T>(ref T obj);

	/// <summary>
	/// This list is thread-safe.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	[Serializable]
	public class TSList<T> : ITSList<T>
	{
		[NonSerialized]
		private PropertyDescriptor sortProperty;
		private ListSortDirection sortDirection;

		//
		// Summary:
		//     Gets whether the list supports searching using the System.ComponentModel.IBindingList.Find(System.ComponentModel.PropertyDescriptor,System.Object)
		//     method.
		//
		// Returns:
		//     true if the list supports searching using the System.ComponentModel.IBindingList.Find(System.ComponentModel.PropertyDescriptor,System.Object)
		//     method; otherwise, false.
		public bool SupportsSearching
		{
			get { return true; }
		}

		// Summary:
		//     Adds the System.ComponentModel.PropertyDescriptor to the indexes used for
		//     searching.
		//
		// Parameters:
		//   property:
		//     The System.ComponentModel.PropertyDescriptor to add to the indexes used for
		//     searching.
		public void AddIndex(PropertyDescriptor property)
		{
		}

		//
		// Summary:
		//     Removes the System.ComponentModel.PropertyDescriptor from the indexes used
		//     for searching.
		//
		// Parameters:
		//   property:
		//     The System.ComponentModel.PropertyDescriptor to remove from the indexes used
		//     for searching.
		public void RemoveIndex(PropertyDescriptor property)
		{
		}

		//
		// Summary:
		//     Returns the index of the row that has the given System.ComponentModel.PropertyDescriptor.
		//
		// Parameters:
		//   property:
		//     The System.ComponentModel.PropertyDescriptor to search on.
		//
		//   key:
		//     The value of the property parameter to search for.
		//
		// Returns:
		//     The index of the row that has the given System.ComponentModel.PropertyDescriptor.
		//
		// Exceptions:
		//   System.NotSupportedException:
		//     System.ComponentModel.IBindingList.SupportsSearching is false.
		public int Find(PropertyDescriptor property, object key)
		{
			return items.Read(lr =>
			{
				for (int i = 0; i < lr.Value.Count; i++)
				{
					T item = lr.Value[i];
					if (property.GetValue(item).Equals(key))
					{
						return i;
					}
				}

				return -1;
			});
		}

		private bool _supportsSorting = true;
		//
		// Summary:
		//     Gets whether the list supports sorting.
		//
		// Returns:
		//     true if the list supports sorting; otherwise, false.
		public bool SupportsSorting
		{
			get
			{
				return _supportsSorting;
			}
			set
			{
				_supportsSorting = value;
			}
		}

		[NonSerialized]
		private PropertyDescriptorCollection _properties = null;
		private PropertyDescriptorCollection properties
		{
			get
			{
				if (_properties == null)
				{
					_properties = TypeDescriptor.GetProperties(typeof(T));
				}

				return _properties;
			}
			set
			{
				_properties = value;
			}
		}

		public void ApplySort(string propertyName, ListSortDirection direction)
		{
			PropertyDescriptor propertyDescriptor = properties.Find(propertyName, false);
			ApplySort(propertyDescriptor, direction);
		}

		[Obsolete("List is already thread safe.")]
		public object SyncRoot
		{
			get { return null; }
		}

		//
		// Summary:
		//     Sorts the list based on a System.ComponentModel.PropertyDescriptor and a
		//     System.ComponentModel.ListSortDirection.
		//
		// Parameters:
		//   property:
		//     The System.ComponentModel.PropertyDescriptor to sort by.
		//
		//   direction:
		//     One of the System.ComponentModel.ListSortDirection values.
		//
		// Exceptions:
		//   System.NotSupportedException:
		//     System.ComponentModel.IBindingList.SupportsSorting is false.
		public void ApplySort(PropertyDescriptor property, ListSortDirection direction)
		{
			items.Write(lw =>
			{
				sortProperty = property;
				sortDirection = direction;
				isSorted = true;
				applySort(property, direction);
			});
		}

		private void applySort(PropertyDescriptor property, ListSortDirection direction)
		{
			items.Value.Sort(GetComparison(property, direction));
		}

		private static Type comparableType = typeof(IComparable);

		public Comparison<T> GetComparison(PropertyDescriptor propertyDescriptor, ListSortDirection direction)
		{
			Comparison<object> valueComparer = null;

			if (comparableType.IsAssignableFrom(propertyDescriptor.PropertyType))
			{
				valueComparer = (obj1, obj2) => ((IComparable)obj1).CompareTo(obj2);
			}
			else
			{
				var mi = propertyDescriptor.PropertyType.GetMethod("CompareTo", new[] { propertyDescriptor.PropertyType });
				if (mi != null)
					valueComparer = (obj1, obj2) => (int)mi.Invoke(obj1, new object[] { obj2 });
				else
					valueComparer = (obj1, obj2) => 0;
			}

			return (item1, item2) =>
			{
				int rez = NullCompare(item1, item2);
				if (rez == -2)
				{
					var obj1 = propertyDescriptor.GetValue(item1);
					var obj2 = propertyDescriptor.GetValue(item2);
					if ((rez = NullCompare(obj1, obj2)) == -2)
					{
						rez = valueComparer(obj1, obj2);
					}
				}
				return direction == ListSortDirection.Descending ? -rez : rez;
			};
		}

		public static int NullCompare(object obj1, object obj2)
		{
			if (obj1 == null && obj2 == null)
			{
				return 0;
			}
			else if (obj1 == null)
			{
				return -1;
			}
			else if (obj2 == null)
			{
				return 1;
			}
			return -2;
		}

		private bool isSorted = false;
		//
		// Summary:
		//     Gets whether the items in the list are sorted.
		//
		// Returns:
		//     true if System.ComponentModel.IBindingList.ApplySort(System.ComponentModel.PropertyDescriptor,System.ComponentModel.ListSortDirection)
		//     has been called and System.ComponentModel.IBindingList.RemoveSort() has not
		//     been called; otherwise, false.
		//
		// Exceptions:
		//   System.NotSupportedException:
		//     System.ComponentModel.IBindingList.SupportsSorting is false.
		public bool IsSorted
		{
			get { return isSorted; }
		}

		//
		// Summary:
		//     Removes any sort applied using System.ComponentModel.IBindingList.ApplySort(System.ComponentModel.PropertyDescriptor,System.ComponentModel.ListSortDirection).
		//
		// Exceptions:
		//   System.NotSupportedException:
		//     System.ComponentModel.IBindingList.SupportsSorting is false.
		public void RemoveSort()
		{
			items.Write(lw =>
			{
				sortProperty = null;
				isSorted = false;
			});
		}

		//
		// Summary:
		//     Gets the direction of the sort.
		//
		// Returns:
		//     One of the System.ComponentModel.ListSortDirection values.
		//
		// Exceptions:
		//   System.NotSupportedException:
		//     System.ComponentModel.IBindingList.SupportsSorting is false.
		public ListSortDirection SortDirection
		{
			get { return sortDirection; }
		}

		//
		// Summary:
		//     Gets the System.ComponentModel.PropertyDescriptor that is being used for
		//     sorting.
		//
		// Returns:
		//     The System.ComponentModel.PropertyDescriptor that is being used for sorting.
		//
		// Exceptions:
		//   System.NotSupportedException:
		//     System.ComponentModel.IBindingList.SupportsSorting is false.
		public PropertyDescriptor SortProperty
		{
			get { return sortProperty; }
		}

		private ReaderWriterLockWrapper<List<T>> _items;
		private ReaderWriterLockWrapper<List<T>> items
		{
			get
			{
				return _items;
			}
			set
			{
				_items = value;
			}
		}

		public delegate T GetNewEventHandler();
		private Valuent<GetNewEventHandler> _getNew;
		/// <summary>
		/// Method should return new item instance.
		/// </summary>
		public GetNewEventHandler GetNew
		{
			get
			{
				if (_getNew == null && constructorOfT.Value != null)
				{
					_getNew = new Valuent<GetNewEventHandler>(getNewT);
				}

				return _getNew;
			}
			set
			{
				_getNew = value;
			}
		}

		private Valuent<ConstructorInfo> _constructorOfT;
		private Valuent<ConstructorInfo> constructorOfT
		{
			get
			{
				if (_constructorOfT == null)
				{
					_constructorOfT = new Valuent<ConstructorInfo>(typeof(T).GetConstructor(Type.EmptyTypes));
				}
				return _constructorOfT;
			}
		}

		private T getNewT()
		{
			return (T)constructorOfT.Value.Invoke(null);
		}

		[field: NonSerialized]
		public event ListChangedEventHandler ListChanged;

		public bool IsListChangedNull()
		{
			return ListChanged == null;
		}

		private void onListChanged(ListChangedType listChangedType, bool removedItemHasValue, T removedItem, bool addedItemHasValue, T addedItem, int itemIndex)
		{
			T[] removedItems = removedItemHasValue ? new T[] { removedItem } : new T[0];
			T[] addedItems = addedItemHasValue ? new T[] { addedItem } : new T[0];
			onListChanged(listChangedType, removedItems, addedItems, new int[] { itemIndex });
		}

		private void onListChanged(ListChangedType listChangedType, T[] removedItems)
		{
			onListChanged(listChangedType, removedItems, new T[0], new int[0]);
		}

		private void onListChanged(ListChangedType listChangedType, T[] addedItems, int[] itemIndexes)
		{
			onListChanged(listChangedType, new T[0], addedItems, itemIndexes);
		}

		private void onListChanged(ListChangedType listChangedType, T[] removedItems, T[] addedItems)
		{
			onListChanged(listChangedType, removedItems, addedItems, new int[0]);
		}

		private void onListChanged(ListChangedType listChangedType, T[] removedItems, T[] addedItems, int[] itemIndexes)
		{
			foreach (T item in removedItems)
			{
				if (item != null && item is INotifyPropertyChanged)
				{
					((INotifyPropertyChanged)item).PropertyChanged -= itemPropertyChanged;
				}
			}

			foreach (T item in addedItems)
			{
				if (item != null && item is INotifyPropertyChanged)
				{
					((INotifyPropertyChanged)item).PropertyChanged += itemPropertyChanged;
				}
			}

			if (IsSorted && addedItems.Length > 0)
			{
				ApplySort(sortProperty, sortDirection);
			}

			if (ListChanged != null)
			{
				if (listChangedType == ListChangedType.ItemAdded)
				{
					for (int i = 0; i < addedItems.Length; i++)
					{
						ListChanged(new ChangeInfo(this, addedItems[i], itemIndexes[i]), new ListChangedEventArgs(listChangedType, itemIndexes[i]));
					}
				}
				else if (removedItems.Length > 0 || addedItems.Length > 0)
				{
					int itemIndex = itemIndexes.Length == 1 ? itemIndexes[0] : -1;
					ListChanged(new ChangeInfo(this, removedItems, addedItems, itemIndexes), new ListChangedEventArgs(listChangedType, itemIndex));
				}
			}
		}

		private void itemPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (IsSorted && sortProperty.Name == e.PropertyName)
			{
				ApplySort(sortProperty, sortDirection);
			}

			if (ListChanged != null)
			{
				PropertyDescriptor propertyDescriptor = TypeDescriptor.GetProperties(sender.GetType()).Find(e.PropertyName, false);
				int index = IndexOf(sender);
				ListChanged(new ChangeInfo(this, new T[] { (T)sender }, new int[] { index }, e.PropertyName), new ListChangedEventArgs(ListChangedType.ItemChanged, index, propertyDescriptor));
			}
		}

		public void ReBindToNotifyPropertyChangedItems()
		{
			items.Read(lr =>
			{
				foreach (T item in lr.Value)
				{
					if (item != null && item is INotifyPropertyChanged)
					{
						INotifyPropertyChanged npcItem = (INotifyPropertyChanged)item;
						npcItem.PropertyChanged -= itemPropertyChanged;
						npcItem.PropertyChanged += itemPropertyChanged;
					}
				}
			});
		}

		//
		// Summary:
		//     Retrieves all the elements that match the conditions defined by the specified
		//     predicate.
		//
		// Parameters:
		//   match:
		//     The System.Predicate<T> delegate that defines the conditions of the elements
		//     to search for.
		//
		// Returns:
		//     A System.Collections.Generic.List<T> containing all the elements that match
		//     the conditions defined by the specified predicate, if found; otherwise, an
		//     empty System.Collections.Generic.List<T>.
		//
		// Exceptions:
		//   System.ArgumentNullException:
		//     match is null.
		public List<T> FindAll(Predicate<T> match)
		{
			List<T> foundList = new List<T>();
			foreach (T item in ToArray())
			{
				if (match(item))
				{
					foundList.Add(item);
				}
			}

			return foundList;
		}

		//
		// Summary:
		//     Returns an System.Collections.ArrayList which represents a subset of the
		//     elements in the source System.Collections.ArrayList.
		//
		// Parameters:
		//   index:
		//     The zero-based System.Collections.ArrayList index at which the range starts.
		//
		//   count:
		//     The number of elements in the range.
		//
		// Returns:
		//     An System.Collections.ArrayList which represents a subset of the elements
		//     in the source System.Collections.ArrayList.
		//
		// Exceptions:
		//   System.ArgumentOutOfRangeException:
		//     index is less than zero.  -or- count is less than zero.
		//
		//   System.ArgumentException:
		//     index and count do not denote a valid range of elements in the System.Collections.ArrayList.
		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public List<T> GetRange(int index, int count)
		{
			return items.Read(lr => lr.Value.GetRange(index, count));
		}

		public void ForEach(Action<T> action)
		{
			foreach (T item in ToArray())
			{
				action(item);
			}
		}

		public List<T> ForEach(ReturnAction<T> action)
		{
			List<T> list = new List<T>();
			foreach (T item in ToArray())
			{
				T newItem = item;
				if (action(ref newItem))
				{
					list.Add(newItem);
				}
			}

			return list;
		}

		public ReaderWriterLockWrapper<List<Tuple<T, int>>> AddNewItems
		{
			get;
			private set;
		}

		public object AddNew()
		{
			if (AllowNew)
			{
				var newItem = GetNew();
				ReadWriteLock.Lock(new[] { AddNewItems.W, items.R }
					, () => AddNewItems.Value.Add(new Tuple<T, int>(newItem, items.Value.Count)));
				Add(newItem);
				return newItem;
			}

			throw new NotSupportedException("System.ComponentModel.IBindingList.AllowNew is false.");
		}

		public TSList(int? capacity = null)
		{
			AllowEdit = true;
			AllowRemove = true;
			IsReadOnly = false;
			IsFixedSize = false;
			AddNewItems = new ReaderWriterLockWrapper<List<Tuple<T, int>>>(new List<Tuple<T, int>>());
			if (capacity.HasValue)
				items = new ReaderWriterLockWrapper<List<T>>(new List<T>(capacity.Value));
			else
				items = new ReaderWriterLockWrapper<List<T>>(new List<T>());
		}

		public TSList(ListChangedEventHandler listChangedEventHandler)
			: this()
		{
			if (listChangedEventHandler != null)
			{
				this.ListChanged += listChangedEventHandler;
			}
		}

		public TSList(IEnumerable<T> collection)
			: this(collection, null)
		{
		}

		public TSList(IEnumerable<T> collection, ListChangedEventHandler listChangedEventHandler)
			: this()
		{
			if (listChangedEventHandler != null)
			{
				this.ListChanged += listChangedEventHandler;
			}
			AddRange(collection);
		}

		public bool IsSynchronized
		{
			get { return true; }
		}

		public T this[int index]
		{
			get
			{
				return items.Read(lr => lr.Value[index]);
			}
			set
			{
				T oldItem = items.Write(lw =>
				 {
					 oldItem = lw[index];
					 lw[index] = value;
					 return oldItem;
				 });
				onListChanged(ListChangedType.ItemChanged, true, oldItem, true, value, index);
			}
		}

		public void Sort()
		{
			items.Write(lw => items.Value.Sort());
		}

		public void Sort(Comparison<T> comparison)
		{
			items.Write(lw => items.Value.Sort(comparison));
		}

		public void Sort(IComparer<T> comparer)
		{
			items.Write(lw => items.Value.Sort(comparer));
		}

		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void Sort(int index, int count, IComparer<T> comparer)
		{
			items.Write(lw => items.Value.Sort(index, count, comparer));
		}

		object System.Collections.IList.this[int index]
		{
			get
			{
				return this[index];
			}
			set
			{
				this[index] = (T)value;
			}
		}

		public bool SupportsChangeNotification
		{
			get { return true; }
		}

		public bool AllowEdit
		{
			get;
			set;
		}

		public bool AllowNew
		{
			get
			{
				return GetNew != null;
			}
		}

		public bool AllowRemove
		{
			get;
			set;
		}

		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void Insert(int index, T item)
		{
			items.Write(lw =>
			{
				lw.Insert(index, item);
			});
			onListChanged(ListChangedType.ItemAdded, false, default(T), true, item, index);
		}

		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void RemoveAt(int index)
		{
			T removedItem = items.Write(lw => removeAt(index));
			onListChanged(ListChangedType.ItemDeleted, true, removedItem, false, default(T), index);
		}

		public void Add(T item)
		{
			Add((object)item);
		}

		public bool Remove(T item)
		{
			int index = -1;
			bool ret = false;
			items.Write(lw => ret = remove(item, out index));
			onListChanged(ListChangedType.ItemDeleted, true, item, false, default(T), index);

			return ret;
		}

		public bool Contains(T item)
		{
			return items.Read(lr => lr.Value.Contains(item));
		}

		public void Clear()
		{
			T[] oldItems = null;
			items.Write(lw => oldItems = clear());
			onListChanged(ListChangedType.Reset, oldItems);
		}

		public void Replace(IEnumerable<T> collection)
		{
			T[] removedItems = null;
			T[] addedItems = null;
			items.Write(lw =>
			{
				removedItems = lw.ToArray();
				replace(collection);
				addedItems = lw.ToArray();
			});
			onListChanged(ListChangedType.Reset, removedItems, addedItems);
		}

		private void replace(IEnumerable<T> collection)
		{
			items.Value.Clear();
			items.Value.AddRange(collection);
		}

		public int Count
		{
			get
			{
				return items.Read(lr => lr.Value.Count);
			}
		}

		public int IndexOf(T item)
		{
			return items.Read(lr => lr.Value.IndexOf(item));
		}

		public T[] ToArray()
		{
			return items.Read(lr => lr.Value.ToArray());
		}

		public List<T> ToList()
		{
			return items.Read(lr => new List<T>(lr.Value));
		}

		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			items.Read(lr => lr.Value.CopyTo(array, arrayIndex));
		}

		public void AddRange(IEnumerable<T> collection)
		{
			bool reset = false;
			KeyValuePair<int[], T[]> addedItems = items.Write(lw =>
			  {
				  reset = lw.Count == 0;
				  return addRange(collection);
			  });
			onListChanged(reset ? ListChangedType.Reset : ListChangedType.ItemAdded, addedItems.Value, addedItems.Key);
		}

		public void AddRange(IEnumerable collection)
		{
			List<T> newItems = new List<T>();
			foreach (object obj in collection)
			{
				newItems.Add((T)obj);
			}
			AddRange(newItems);
		}

		public bool IsReadOnly
		{
			get;
			set;
		}

		public IEnumerator<T> GetEnumerator()
		{
			return new Enumerator(this);
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public int Add(object value)
		{
			int index = items.Write(lw => add((T)value));
			onListChanged(ListChangedType.ItemAdded, false, default(T), true, (T)value, index);

			return index;
		}

		public bool Contains(object value)
		{
			return Contains((T)value);
		}

		public int IndexOf(object value)
		{
			return IndexOf((T)value);
		}

		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void Insert(int index, object value)
		{
			Insert(index, (T)value);
		}

		public void Remove(object value)
		{
			Remove((T)value);
		}

		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void CopyTo(Array array, int index)
		{
			items.Write(lw => lw.ToArray().CopyTo(array, index));
		}

		public bool IsFixedSize
		{
			get;
			set;
		}

		private T removeAt(int index)
		{
			T removedItem = items.Value[index];
			items.Value.RemoveAt(index);
			return removedItem;
		}

		private T[] clear()
		{
			var oldItems = items.Value.ToArray();
			items.Value.Clear();
			return oldItems;
		}

		private int add(T item)
		{
			int index = items.Value.Count;
			items.Value.Insert(index, item);
			return index;
		}

		private bool remove(T item, out int index)
		{
			for (int i = 0; i < items.Value.Count; i++)
			{
				if (object.Equals(item, items.Value[i]))
				{
					items.Value.RemoveAt(i);
					index = i;
					return true;
				}
			}

			index = -1;
			return false;
		}

		private KeyValuePair<int[], T[]> addRange(IEnumerable<T> collection)
		{
			var fromIndex = items.Value.Count;
			items.Value.AddRange(collection);
			var addedItemIndexes = new List<int>();
			var addedItems = new List<T>();

			for (int i = fromIndex; i < items.Value.Count; i++)
			{
				addedItemIndexes.Add(i);
				addedItems.Add(items.Value[i]);
			}

			return new KeyValuePair<int[], T[]>(addedItemIndexes.ToArray(), addedItems.ToArray());
		}

		private class Enumerator : IEnumerator<T>
		{
			private TSList<T> tsList;
			private T[] list;
			private T current;
			private int index = -1;

			public Enumerator(TSList<T> tsList)
			{
				this.tsList = tsList;
				list = tsList.ToArray();
			}

			public T Current
			{
				get
				{
					if (index == -1 || list.Length <= index)
					{
						throw new InvalidOperationException("Enumeration has either not started or has already finished.");
					}
					return current;
				}
			}

			object System.Collections.IEnumerator.Current
			{
				get
				{
					if (index == -1 || list.Length <= index)
					{
						throw new InvalidOperationException("Enumeration has either not started or has already finished.");
					}
					return current;
				}
			}

			public void Dispose()
			{
				tsList = null;
				list = null;
				current = default(T);
			}

			public bool MoveNext()
			{
				index++;
				if (list.Length > index)
				{
					current = list[index];
					return true;
				}

				return false;
			}

			public void Reset()
			{
				index = -1;
				current = default(T);
				list = tsList.ToArray();
			}
		}

		public bool RaisesItemChangedEvents
		{
			get { return true; }
		}

		public struct ChangeInfo
		{
			public TSList<T> ItemList;
			public T[] AddedItems;
			public T[] RemovedItems;
			public T[] ChangedItems;
			public int[] ItemIndexes;
			public string PropertyName;

			public ChangeInfo(TSList<T> itemList, T addedItem, int itemIndex)
				: this(itemList, new T[0], new T[] { addedItem }, new int[] { itemIndex })
			{
			}

			public ChangeInfo(TSList<T> itemList, T[] changedItems, int[] itemIndexes)
				: this(itemList, new T[0], new T[0], changedItems, itemIndexes, "")
			{
			}

			public ChangeInfo(TSList<T> itemList, T[] changedItems, int[] itemIndexes, string PropertyName)
				: this(itemList, new T[0], new T[0], changedItems, itemIndexes, PropertyName)
			{
			}

			public ChangeInfo(TSList<T> itemList, T[] removedItems, T[] addedItems, int[] itemIndexes)
				: this(itemList, removedItems, addedItems, new T[0], itemIndexes, "")
			{
			}

			public ChangeInfo(TSList<T> itemList, T[] removedItems, T[] addedItems, T[] changedItems, int[] itemIndexes, string propertyName)
			{
				ItemList = itemList;
				RemovedItems = removedItems;
				AddedItems = addedItems;
				ChangedItems = changedItems;
				ItemIndexes = itemIndexes;
				PropertyName = propertyName;
			}

			public static implicit operator TSList<T>(ChangeInfo descriptor)
			{
				return descriptor.ItemList;
			}
		}

		public object Clone()
		{
			return items.Read(lr => new TSList<T>(lr.Value));
		}

		public void Sort(IComparer comparer)
		{
			items.Write(lw =>
			{
				ArrayList list = new ArrayList(lw);
				list.Sort(comparer);
				lw.Clear();
				lw.AddRange((T[])list.ToArray(typeof(T)));
			});
		}

		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void Sort(int index, int count, IComparer comparer)
		{
			items.Write(lw =>
			{
				ArrayList list = new ArrayList(lw);
				list.Sort(index, count, comparer);
				lw.Clear();
				lw.AddRange((T[])list.ToArray(typeof(T)));
			});
		}

		public string Filter { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public ListSortDescriptionCollection SortDescriptions => throw new NotImplementedException();

		public bool SupportsAdvancedSorting
		{
			get
			{
				return false;
			}
		}

		public bool SupportsFiltering
		{
			get
			{
				return false;
			}
		}

		public void ApplySort(ListSortDescriptionCollection sorts)
		{
			throw new NotImplementedException();
		}

		public void RemoveFilter()
		{
			filter = null;
		}

		private Predicate<T> filter;
		public void ApplyFilter(Predicate<T> filter)
		{
			this.filter = filter;
		}

		public void CancelNew(int itemIndex)
		{
			AddNewItems.Read(lr =>
			{
				items.Read(ir =>
				{
					var indexesToRemove = new List<int>();
					foreach (var i in lr.Value)
					{
						if (ir.Value[i.Item2].Equals(i.Item1))
							indexesToRemove.Add(i.Item2);
						else
						{
							var idx = ir.Value.IndexOf(i.Item1);
							if (idx > -1)
								indexesToRemove.Add(idx);
						}
					}

					indexesToRemove.Sort();
					indexesToRemove.Reverse();
					ir.Write(iw => indexesToRemove.ForEach(i => iw.Value.RemoveAt(i)));
				});

				lr.Write(lw => lw.Value.Clear());
			});
		}

		public void EndNew(int itemIndex)
		{
			AddNewItems.Write(lw => lw.Clear());
		}
	}
}
