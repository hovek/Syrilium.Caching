using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Syrilium.CommonInterface
{
	public class ObservableCollectionReplication<TCollection1, TCollection2> where TCollection2 : TCollection1
	{
		private ObservableCollection<TCollection1> collection1;
		public ObservableCollection<TCollection1> Collection1
		{
			get
			{
				return collection1;
			}
			set
			{
				if (collection1 != null)
				{
					collection1.CollectionChanged -= collection1_CollectionChanged;
				}
				collection1 = value;
				collection1.CollectionChanged += collection1_CollectionChanged;
				copyCollection1ToCollection2();
			}
		}
		private ObservableCollection<TCollection2> collection2;
		public ObservableCollection<TCollection2> Collection2
		{
			get
			{
				return collection2;
			}
			set
			{
				if (collection2 != null)
				{
					collection2.CollectionChanged -= collection2_CollectionChanged;
				}
				collection2 = value;
				collection2.CollectionChanged += collection2_CollectionChanged;
				copyCollection1ToCollection2();
			}
		}

		private bool editingCollection1 = false;
		private bool editingCollection2 = false;

		public ObservableCollectionReplication()
		{
		}

		public ObservableCollectionReplication(ObservableCollection<TCollection1> collection1, ObservableCollection<TCollection2> collection2)
		{
			Collection1 = collection1;
			Collection2 = collection2;
		}

		private void copyCollection1ToCollection2()
		{
			editingCollection2 = true;
			try
			{
				if (Collection2 != null)
				{
					Collection2.Clear();
					foreach (TCollection2 item in Collection1)
					{
						Collection2.Add(item);
					}
				}
			}
			finally
			{
				editingCollection2 = false;
			}
		}

		private void collection1_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (editingCollection1)
			{
				return;
			}

			editingCollection2 = true;
			try
			{
				if (e.OldItems != null)
				{
					for (int i = 0; i < e.OldItems.Count; i++)
					{
						Collection2.RemoveAt(e.OldStartingIndex);
					}
				}

				if (e.NewItems != null)
				{
					int start = e.NewStartingIndex > Collection2.Count ? Collection2.Count : e.NewStartingIndex;
					int end = e.NewStartingIndex + 1;
					for (int i = start; i < end; i++)
					{
						Collection2.Insert(i, (TCollection2)Collection1[i]);
					}
				}
			}
			finally
			{
				editingCollection2 = false;
			}
		}

		private void collection2_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (editingCollection2)
			{
				return;
			}

			editingCollection1 = true;
			try
			{
				if (e.OldItems != null)
				{
					for (int i = 0; i < e.OldItems.Count; i++)
					{
						Collection1.RemoveAt(e.OldStartingIndex);
					}
				}

				if (e.NewItems != null)
				{
					int start = e.NewStartingIndex > Collection1.Count ? Collection1.Count : e.NewStartingIndex;
					int end = e.NewStartingIndex + 1;
					for (int i = start; i < end; i++)
					{
						Collection1.Insert(i, (TCollection1)Collection2[i]);
					}
				}
			}
			finally
			{
				editingCollection1 = false;
			}
		}
	}
}
