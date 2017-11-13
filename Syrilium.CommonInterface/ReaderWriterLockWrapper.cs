using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Syrilium.CommonInterface
{
	public class ReaderWriterLockWrapper
	{
		internal ReaderWriterLockSlim mainLock = new ReaderWriterLockSlim();

		protected object value;

		public object Value
		{
			get
			{
				return value;
			}
		}

		//
		// Summary:
		//     Gets a value indicating whether the current thread holds a reader lock.
		//
		// Returns:
		//     true if the current thread holds a reader lock; otherwise, false.
		public bool IsReaderLockHeld
		{
			get
			{
				return mainLock.IsReadLockHeld;
			}
		}
		//
		// Summary:
		//     Gets a value indicating whether the current thread holds the writer lock.
		//
		// Returns:
		//     true if the current thread holds the writer lock; otherwise, false.
		public bool IsWriterLockHeld
		{
			get
			{
				return mainLock.IsWriteLockHeld;
			}
		}

		public bool IsUpgradeableReadLockHeld
		{
			get
			{
				return mainLock.IsUpgradeableReadLockHeld;
			}
		}

		public void EnterUpgradeableReadLock()
		{
			mainLock.EnterUpgradeableReadLock();
		}

		public void ExitUpgradeableReadLock()
		{
			mainLock.ExitUpgradeableReadLock();
		}

		public void AcquireReaderLock()
		{
			mainLock.EnterReadLock();
		}

		public void ReleaseReaderLock()
		{
			mainLock.ExitReadLock();
		}

		public void AcquireWriterLock()
		{
			var hadReadWriteLock = mainLock.IsUpgradeableReadLockHeld;
			mainLock.EnterWriteLock();
			checkWriteAccess(hadReadWriteLock);
		}

		public void ReleaseWriterLock()
		{
			mainLock.ExitWriteLock();
		}

		protected void checkWriteAccess(bool hadReadWriteLock)
		{
			if (hadReadWriteLock)
				throw new InvalidOperationException("There is already ReadWrite lock on this thread.");
		}

		internal virtual UpgradeToWriterLockWrapper GetUpgradeToWriterLockWrapper()
		{
			return new UpgradeToWriterLockWrapper(mainLock, value);
		}
	}

	public class ReaderWriterLockWrapper<T> : ReaderWriterLockWrapper
	{
		new public T Value
		{
			get
			{
				return (T)value;
			}
		}

		private KeyValuePair<LockType, ReaderWriterLockWrapper>? write;
		public KeyValuePair<LockType, ReaderWriterLockWrapper> W
		{
			get
			{
				if (write == null)
					write = new KeyValuePair<LockType, ReaderWriterLockWrapper>(LockType.Write, this);
				return write.Value;
			}
		}

		private KeyValuePair<LockType, ReaderWriterLockWrapper>? read;
		public KeyValuePair<LockType, ReaderWriterLockWrapper> R
		{
			get
			{
				if (read == null)
					read = new KeyValuePair<LockType, ReaderWriterLockWrapper>(LockType.Read, this);
				return read.Value;
			}
		}

		private KeyValuePair<LockType, ReaderWriterLockWrapper>? readWrite;
		public KeyValuePair<LockType, ReaderWriterLockWrapper> RW
		{
			get
			{
				if (readWrite == null)
					readWrite = new KeyValuePair<LockType, ReaderWriterLockWrapper>(LockType.ReadWrite, this);
				return readWrite.Value;
			}
		}

		public ReaderWriterLockWrapper()
		{
			this.value = (T)typeof(T).GetConstructor(Type.EmptyTypes).Invoke(null);
			init();
		}

		public ReaderWriterLockWrapper(T value)
		{
			this.value = value;
			init();
		}

		private void init()
		{
		}

		public void Read(Action<T> action)
		{
			mainLock.EnterReadLock();
			try
			{
				action(Value);
			}
			finally
			{
				mainLock.ExitReadLock();
			}
		}

		public TRes Read<TRes>(Func<T, TRes> func)
		{
			mainLock.EnterReadLock();
			try
			{
				return func(Value);
			}
			finally
			{
				mainLock.ExitReadLock();
			}
		}

		public void ReadWrite(Action<UpgradeToWriterLockWrapper<T>> action)
		{
			mainLock.EnterUpgradeableReadLock();
			try
			{
				using (var upgradeToWriter = (UpgradeToWriterLockWrapper<T>)GetUpgradeToWriterLockWrapper())
					action(upgradeToWriter);
			}
			finally
			{
				mainLock.ExitUpgradeableReadLock();
			}
		}

		public TRes ReadWrite<TRes>(Func<UpgradeToWriterLockWrapper<T>, TRes> func)
		{
			mainLock.EnterUpgradeableReadLock();
			try
			{
				using (var upgradeToWriter = (UpgradeToWriterLockWrapper<T>)GetUpgradeToWriterLockWrapper())
					return func(upgradeToWriter);
			}
			finally
			{
				mainLock.ExitUpgradeableReadLock();
			}
		}

		public void Write(Action<T> action)
		{
			var hadReadWriteLock = mainLock.IsUpgradeableReadLockHeld;
			mainLock.EnterWriteLock();
			try
			{
				checkWriteAccess(hadReadWriteLock);
				action(Value);
			}
			finally
			{
				mainLock.ExitWriteLock();
			}
		}

		public TRes Write<TRes>(Func<T, TRes> func)
		{
			var hadReadWriteLock = mainLock.IsUpgradeableReadLockHeld;
			mainLock.EnterWriteLock();
			try
			{
				checkWriteAccess(hadReadWriteLock);
				return func(Value);
			}
			finally
			{
				mainLock.ExitWriteLock();
			}
		}

		public void ConditionalReadWrite(Func<T, bool> writeCondition, Action<T> read, Action<T> write)
		{
			ConditionalReadWrite(
				writeCondition,
				v => { read(v); return default(T); },
				v => { write(v); return default(T); }
				);
		}

		public TRes ConditionalReadWrite<TRes>(Func<T, bool> writeCondition, Func<T, TRes> read, Func<T, TRes> write)
		{
			return ReadWrite(v =>
				{
					if (!writeCondition(v.Value))
						return read(v.Value);
					else
					{
						return v.Write(value =>
							{
								if (writeCondition(v.Value))
									return write(value);
								else
									return read(value);
							});
					}
				});
		}

		internal override UpgradeToWriterLockWrapper GetUpgradeToWriterLockWrapper()
		{
			return new UpgradeToWriterLockWrapper<T>(mainLock, Value);
		}
	}

	public class UpgradeToWriterLockWrapper<T> : UpgradeToWriterLockWrapper
	{
		new public T Value
		{
			get
			{
				return (T)value;
			}
		}

		public UpgradeToWriterLockWrapper(ReaderWriterLockSlim mainLock, T value) :
			base(mainLock, value)
		{
		}

		public void Write(Action<T> action)
		{
			base.Write(action);
		}

		public TRes Write<TRes>(Func<T, TRes> func)
		{
			return base.Write(func);
		}
	}

	public class UpgradeToWriterLockWrapper : IDisposable
	{
		protected ReaderWriterLockSlim mainLock;
		protected object value;

		public object Value
		{
			get
			{
				return value;
			}
		}

		public UpgradeToWriterLockWrapper(ReaderWriterLockSlim mainLock, object value)
		{
			this.mainLock = mainLock;
			this.value = value;
		}

		public void Write<T>(Action<T> action)
		{
			mainLock.EnterWriteLock();
			try
			{
				action((T)Value);
			}
			finally
			{
				mainLock.ExitWriteLock();
			}
		}

		public TRes Write<TRes, T>(Func<T, TRes> func)
		{
			mainLock.EnterWriteLock();
			try
			{
				return func((T)Value);
			}
			finally
			{
				mainLock.ExitWriteLock();
			}
		}

		public void Dispose()
		{
			IsDisposed = true;
			value = null;
			mainLock = null;
		}

		public bool IsDisposed { get; private set; }
	}

	public enum LockType
	{
		Read,
		Write,
		ReadWrite,
	}

	public static class ReadWriteLock
	{
		public static void Lock(IEnumerable<KeyValuePair<LockType, ReaderWriterLockWrapper>> readWriteLocks, Action<UpgradeToWriteLock> action)
		{
			try
			{
				var locks = new List<ReaderWriterLockWrapper>();
				foreach (var rwLock in readWriteLocks)
				{
					if (rwLock.Key == LockType.Write)
						rwLock.Value.AcquireWriterLock();
					else if (rwLock.Key == LockType.Read)
						rwLock.Value.AcquireReaderLock();
					else
					{
						locks.Add(rwLock.Value);
						rwLock.Value.EnterUpgradeableReadLock();
					}
				}
				if (locks.Count > 0)
				{
					using (var upgradeToWriteLock = new UpgradeToWriteLock(locks))
						action(upgradeToWriteLock);
				}
				else
					action(null);
			}
			finally
			{
				foreach (var rwLock in readWriteLocks)
				{
					if (rwLock.Key == LockType.Write)
						rwLock.Value.ReleaseWriterLock();
					else if (rwLock.Key == LockType.Read)
						rwLock.Value.ReleaseReaderLock();
					else
						rwLock.Value.ExitUpgradeableReadLock();
				}
			}
		}
	}

	public class UpgradeToWriteLock : IDisposable
	{
		private Dictionary<ReaderWriterLockWrapper, UpgradeToWriterLockWrapper> upgradeToWriterLockWrapper = new Dictionary<ReaderWriterLockWrapper, UpgradeToWriterLockWrapper>();

		public UpgradeToWriteLock(IEnumerable<ReaderWriterLockWrapper> readWriteLocks)
		{
			foreach (var lck in readWriteLocks)
				upgradeToWriterLockWrapper.Add(lck, lck.GetUpgradeToWriterLockWrapper());
		}

		public UpgradeToWriterLockWrapper this[ReaderWriterLockWrapper key]
		{
			get
			{
				return upgradeToWriterLockWrapper[key];
			}
		}

		public void Write<T>(ReaderWriterLockWrapper<T> v, Action<T> action)
		{
			upgradeToWriterLockWrapper[v].Write(action);
		}

		public TRes Write<T, TRes>(ReaderWriterLockWrapper<T> v, Func<T, TRes> func)
		{
			return upgradeToWriterLockWrapper[v].Write(func);
		}

		public void Dispose()
		{
			IsDisposed = true;
			foreach (var l in upgradeToWriterLockWrapper)
				l.Value.Dispose();
			upgradeToWriterLockWrapper = null;
		}

		public bool IsDisposed { get; private set; }
	}
}
