using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Collections;
using System.Reflection;
using Syrilium.CommonInterface;
using System.Linq;

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
			return items.Read(_ =>
			{
				for (int i = 0; i < items.Value.Count; i++)
				{
					T item = items.Value[i];
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
			items.Write(_ =>
			{
				sortProperty = property;
				sortDirection = direction;
				isSorted = true;
				applySort(property, direction);
			});
		}

		private void applySort(PropertyDescriptor property, ListSortDirection direction)
		{
			var comparison = GetComparison(property, direction);
			if (itemsView != null)
				itemsView.Sort(comparison);
			items.Value.Sort(comparison);
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
			items.Write(_ =>
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

		private ReaderWriterLockWrapper<List<T>> items;
		private List<T> _itemsView;

		private List<T> itemsView
		{
			get
			{
				return _itemsView;
			}
			set
			{
				_itemsView = value;
				if (_itemsView == null)
					currentItems = items.Value;
				else
					currentItems = _itemsView;
			}
		}

		private List<T> currentItems;

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

		private void onListChanged(ListChangedType listChangedType, bool removedItemHasValue, T removedItem, bool addedItemHasValue, T addedItem, int itemIndex, object extraInfo = null)
		{
			T[] removedItems = removedItemHasValue ? new T[] { removedItem } : new T[0];
			T[] addedItems = addedItemHasValue ? new T[] { addedItem } : new T[0];
			onListChanged(listChangedType, removedItems, addedItems, new int[] { itemIndex }, extraInfo);
		}

		private void onListChanged(ListChangedType listChangedType, T[] removedItems, object extraInfo = null)
		{
			onListChanged(listChangedType, removedItems, new T[0], new int[0], extraInfo);
		}

		private void onListChanged(ListChangedType listChangedType, T[] addedItems, int[] itemIndexes, object extraInfo = null)
		{
			onListChanged(listChangedType, new T[0], addedItems, itemIndexes, extraInfo);
		}

		private void onListChanged(ListChangedType listChangedType, T[] removedItems, T[] addedItems, object extraInfo = null)
		{
			onListChanged(listChangedType, removedItems, addedItems, new int[0], extraInfo);
		}

		private void onListChanged(ListChangedType listChangedType, T[] removedItems, T[] addedItems, int[] itemIndexes, object extraInfo = null)
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

			if (addedItems.Length > 0)
			{
				if (filter != null)
					filterAddedItems(addedItems);
				if (IsSorted)
					ApplySort(sortProperty, sortDirection);
			}

			if (ListChanged != null)
			{
				if (listChangedType == ListChangedType.ItemAdded)
				{
					for (int i = 0; i < addedItems.Length; i++)
					{
						ListChanged(new ChangeInfo(this, addedItems[i], itemIndexes[i], extraInfo), new ListChangedEventArgs(listChangedType, itemIndexes[i]));
					}
				}
				else if (removedItems.Length > 0 || addedItems.Length > 0)
				{
					int itemIndex = itemIndexes.Length == 1 ? itemIndexes[0] : -1;
					ListChanged(new ChangeInfo(this, removedItems, addedItems, itemIndexes, extraInfo), new ListChangedEventArgs(listChangedType, itemIndex));
				}
			}
		}

		private void itemPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (sortProperty.Name == e.PropertyName)
			{
				if (filter != null)
					filterAddedItems(new[] { (T)sender });
				if (IsSorted)
					ApplySort(sortProperty, sortDirection);
			}

			if (ListChanged != null)
			{
				PropertyDescriptor propertyDescriptor = TypeDescriptor.GetProperties(sender.GetType()).Find(e.PropertyName, false);
				int index = IndexOf(sender);
				ListChanged(new ChangeInfo(this, new T[] { (T)sender }, new int[] { index }, e.PropertyName), new ListChangedEventArgs(ListChangedType.ItemChanged, index, propertyDescriptor));
			}
		}

		private void filterAddedItems(IEnumerable<T> addedItems)
		{
			items.Write(_ =>
			{
				foreach (var ai in addedItems)
				{
					if (!filter(ai))
						itemsView.Remove(ai);
				}
			});
		}

		public void ReBindToNotifyPropertyChangedItems()
		{
			items.Read(_ =>
			{
				foreach (T item in items.Value)
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
			return items.Read(_ => currentItems.GetRange(index, count));
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

		private ReaderWriterLockWrapper<List<T>> addNewItems
		{
			get;
			set;
		}

		public object AddNew()
		{
			if (AllowNew)
			{
				var newItem = GetNew();
				int itemIndex = -1;
				ReadWriteLock.Lock(new[] { addNewItems.W, items.W }
					, () =>
					{
						addNewItems.Value.Add(newItem);
						itemIndex = add(newItem);
					});
				onItemAdded(newItem, itemIndex, "AddNewItem");

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
			addNewItems = new ReaderWriterLockWrapper<List<T>>(new List<T>());
			if (capacity.HasValue)
				items = new ReaderWriterLockWrapper<List<T>>(new List<T>(capacity.Value));
			else
				items = new ReaderWriterLockWrapper<List<T>>(new List<T>());
			currentItems = items.Value;
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
				return items.Read(_ => currentItems[index]);
			}
			set
			{
				T oldItem = items.Write(_ =>
				 {
					 oldItem = currentItems[index];
					 if (itemsView != null)
					 {
						 var itmIdx = items.Value.IndexOf(oldItem);
						 items.Value[itmIdx] = value;
					 }
					 currentItems[index] = value;
					 return oldItem;
				 });
				onListChanged(ListChangedType.ItemChanged, true, oldItem, true, value, index);
			}
		}

		public void Sort()
		{
			items.Write(_ =>
			{
				items.Value.Sort();
				itemsView?.Sort();
			});
		}

		public void Sort(Comparison<T> comparison)
		{
			items.Write(_ =>
			{
				items.Value.Sort(comparison);
				itemsView?.Sort(comparison);
			});
		}

		public void Sort(IComparer<T> comparer)
		{
			items.Write(_ =>
			{
				items.Value.Sort(comparer);
				itemsView?.Sort(comparer);
			});
		}

		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void Sort(int index, int count, IComparer<T> comparer)
		{
			items.Write(_ =>
			{
				items.Value.Sort(index, count, comparer);
				itemsView?.Sort(index, count, comparer);
			});
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
			items.Write(_ =>
			{
				currentItems.Insert(index, item);
				if (itemsView != null)
				{
					int itemsIdx;
					if (index < currentItems.Count)
					{
						var itm = currentItems[index];
						itemsIdx = items.Value.IndexOf(itm);
					}
					else
						itemsIdx = index;
					items.Value.Insert(itemsIdx, item);
				}
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
			T removedItem = items.Write(_ => removeAt(index));
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
			items.Write(_ => ret = remove(item, out index));
			onListChanged(ListChangedType.ItemDeleted, true, item, false, default(T), index);

			return ret;
		}

		public bool Contains(T item)
		{
			return items.Read(_ => items.Value.Contains(item));
		}

		public void Clear()
		{
			T[] oldItems = null;
			items.Write(_ => oldItems = clear());
			onListChanged(ListChangedType.Reset, oldItems);
		}

		public void Replace(IEnumerable<T> collection)
		{
			T[] removedItems = null;
			T[] addedItems = null;
			items.Write(_ =>
			{
				removedItems = items.Value.ToArray();
				replace(collection);
				addedItems = items.Value.ToArray();
			});
			onListChanged(ListChangedType.Reset, removedItems, addedItems);
		}

		private void replace(IEnumerable<T> collection)
		{
			items.Value.Clear();
			items.Value.AddRange(collection);
			if (itemsView != null)
			{
				itemsView.Clear();
				itemsView.AddRange(collection);
			}
		}

		public int Count
		{
			get
			{
				return items.Read(_ => currentItems.Count);
			}
		}

		public int IndexOf(T item)
		{
			return items.Read(_ => currentItems.IndexOf(item));
		}

		public T[] ToArray()
		{
			return items.Read(_ => currentItems.ToArray());
		}

		public List<T> ToList()
		{
			return items.Read(_ => new List<T>(currentItems));
		}

		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			items.Read(_ => currentItems.CopyTo(array, arrayIndex));
		}

		public void AddRange(IEnumerable<T> collection)
		{
			bool reset = false;
			KeyValuePair<int[], T[]> addedItems = items.Write(_ =>
			  {
				  reset = currentItems.Count == 0;
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
			int index = items.Write(_ => add((T)value));
			onItemAdded(value, index);

			return index;
		}

		private void onItemAdded(object value, int index, object extraInfo = null)
		{
			onListChanged(ListChangedType.ItemAdded, false, default(T), true, (T)value, index, extraInfo);
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
			items.Write(_ => currentItems.ToArray().CopyTo(array, index));
		}

		public bool IsFixedSize
		{
			get;
			set;
		}

		private T removeAt(int index)
		{
			T removedItem = currentItems[index];
			currentItems.RemoveAt(index);
			if (itemsView != null)
				items.Value.Remove(removedItem);
			return removedItem;
		}

		private T[] clear()
		{
			var oldItems = items.Value.ToArray();
			items.Value.Clear();
			if (itemsView != null)
				itemsView.Clear();
			return oldItems;
		}

		private int add(T item)
		{
			int index = currentItems.Count;
			currentItems.Add(item);
			if (itemsView != null)
				items.Value.Add(item);
			return index;
		}

		private bool remove(T item, out int index)
		{
			var rez = remove(currentItems, item, out index);
			if (itemsView != null)
			{
				int idxDummy;
				rez |= remove(items.Value, item, out idxDummy);
			}
			return rez;
		}

		private static bool remove(List<T> list, T item, out int index)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (object.Equals(item, list[i]))
				{
					list.RemoveAt(i);
					index = i;
					return true;
				}
			}

			index = -1;
			return false;
		}

		private KeyValuePair<int[], T[]> addRange(IEnumerable<T> collection)
		{
			var fromIndex = currentItems.Count;
			currentItems.AddRange(collection);

			if (itemsView != null)
				items.Value.AddRange(collection);

			var addedItemIndexes = new List<int>();
			var addedItems = new List<T>();

			for (int i = fromIndex; i < currentItems.Count; i++)
			{
				addedItemIndexes.Add(i);
				addedItems.Add(currentItems[i]);
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
			public object ExtraInfo;

			public ChangeInfo(TSList<T> itemList, T addedItem, int itemIndex, object extraInfo = null)
				: this(itemList, new T[0], new T[] { addedItem }, new int[] { itemIndex }, extraInfo)
			{
			}

			public ChangeInfo(TSList<T> itemList, T[] changedItems, int[] itemIndexes, object extraInfo = null)
				: this(itemList, new T[0], new T[0], changedItems, itemIndexes, "", extraInfo)
			{
			}

			public ChangeInfo(TSList<T> itemList, T[] changedItems, int[] itemIndexes, string PropertyName, object extraInfo = null)
				: this(itemList, new T[0], new T[0], changedItems, itemIndexes, PropertyName, extraInfo)
			{
			}

			public ChangeInfo(TSList<T> itemList, T[] removedItems, T[] addedItems, int[] itemIndexes, object extraInfo = null)
				: this(itemList, removedItems, addedItems, new T[0], itemIndexes, "", extraInfo)
			{
			}

			public ChangeInfo(TSList<T> itemList, T[] removedItems, T[] addedItems, T[] changedItems, int[] itemIndexes, string propertyName, object extraInfo = null)
			{
				ItemList = itemList;
				RemovedItems = removedItems;
				AddedItems = addedItems;
				ChangedItems = changedItems;
				ItemIndexes = itemIndexes;
				PropertyName = propertyName;
				ExtraInfo = extraInfo;
			}

			public static implicit operator TSList<T>(ChangeInfo descriptor)
			{
				return descriptor.ItemList;
			}
		}

		public object Clone()
		{
			TSList<T> tsl = null;
			items.Read(_ =>
			{
				tsl = new TSList<T>(items.Value);
				if (itemsView != null)
					tsl.itemsView = new List<T>(itemsView);
			});
			tsl.filter = filter;
			tsl.isSorted = isSorted;
			tsl.sortDirection = sortDirection;
			tsl.sortProperty = sortProperty;
			tsl.SupportsSorting = SupportsSorting;
			tsl.ReBindToNotifyPropertyChangedItems();

			return tsl;
		}

		public void Sort(IComparer comparer)
		{
			items.Write(_ =>
			{
				ArrayList list = new ArrayList(currentItems);
				list.Sort(comparer);
				currentItems.Clear();
				currentItems.AddRange((T[])list.ToArray(typeof(T)));
				if (itemsView != null)
				{
					list = new ArrayList(items.Value);
					list.Sort(comparer);
					items.Value.Clear();
					items.Value.AddRange((T[])list.ToArray(typeof(T)));
				}
			});
		}

		/// <summary>
		/// This method is not thread safe.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void Sort(int index, int count, IComparer comparer)
		{
			items.Write(_ =>
			{
				ArrayList list = new ArrayList(currentItems);
				list.Sort(index, count, comparer);
				currentItems.Clear();
				currentItems.AddRange((T[])list.ToArray(typeof(T)));
				if (itemsView != null)
				{
					list = new ArrayList(items.Value);
					list.Sort(index, count, comparer);
					items.Value.Clear();
					items.Value.AddRange((T[])list.ToArray(typeof(T)));
				}
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
			items.Write(_ =>
			{
				filter = null;
				itemsView = null;
			});
			onListChanged(ListChangedType.Reset, new T[0]);
		}

		private Predicate<T> filter;
		public void ApplyFilter(Predicate<T> filter)
		{
			this.filter = filter;
			applyFilter_Lock(filter);
		}

		public void applyFilter_Lock(Predicate<T> filter)
		{
			items.Write(_ => applyFilter(filter));
			onListChanged(ListChangedType.Reset, new T[0]);
		}

		public void applyFilter(Predicate<T> filter)
		{
			itemsView = new List<T>();
			foreach (var itm in items.Value)
			{
				if (filter(itm))
					itemsView.Add(itm);
			}
		}

		public void CancelNew(int itemIndex)
		{
			var removedItems = new List<T>();
			addNewItems.Read(addNewItems =>
			{
				items.Write(_ =>
				{
					foreach (var i in addNewItems.Value)
					{
						int index;
						if (remove(i, out index))
							removedItems.Add(i);
					}
				});

				addNewItems.Write(_ => addNewItems.Value.Clear());
			});

			onListChanged(ListChangedType.ItemDeleted, removedItems.ToArray(), "AddNewItem");
		}

		public event EventHandler OnEndNew;

		public void EndNew(int itemIndex)
		{
			var item = addNewItems.Write(_ =>
			{
				var itm = addNewItems.Value.FirstOrDefault();
				addNewItems.Value.Clear();
				return itm;
			});
			OnEndNew?.Invoke(this, new TSItemEventArgs(item));
		}

		public class TSItemEventArgs : EventArgs
		{
			public T Item
			{
				get;
				private set;
			}

			public TSItemEventArgs(T item)
			{
				Item = item;
			}
		}
	}
}
