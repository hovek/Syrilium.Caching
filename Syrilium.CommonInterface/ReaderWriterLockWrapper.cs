using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Syrilium.CommonInterface
{
	public interface IReadWriteLock
	{
		void AcquireReaderLock();
		void ReleaseReaderLock();
		void AcquireWriterLock();
		void ReleaseWriterLock();
	}

	public class ReaderWriterLockWrapper<T> : IReadWriteLock
	{
		private ReaderWriterLock mainLock = new ReaderWriterLock();
		private T value;
		private UpgradeToWriterLockWrapper upgradeToWriter;

		public T Value
		{
			get
			{
				return value;
			}
		}

		public int WriterSeqNum
		{
			get
			{
				return mainLock.WriterSeqNum;
			}
		}

		private KeyValuePair<LockType, IReadWriteLock>? write;
		public KeyValuePair<LockType, IReadWriteLock> W
		{
			get
			{
				if (write == null)
					write = new KeyValuePair<LockType, IReadWriteLock>(LockType.Write, this);
				return write.Value;
			}
		}

		private KeyValuePair<LockType, IReadWriteLock>? read;
		public KeyValuePair<LockType, IReadWriteLock> R
		{
			get
			{
				if (read == null)
					read = new KeyValuePair<LockType, IReadWriteLock>(LockType.Read, this);
				return read.Value;
			}
		}

		public bool AnyWritersSince(int seqNum)
		{
			return mainLock.AnyWritersSince(seqNum);
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
			upgradeToWriter = new UpgradeToWriterLockWrapper(this);
		}

		public void Read(Action<UpgradeToWriterLockWrapper> action)
		{
			mainLock.AcquireReaderLock(600000);
			try
			{
				action(upgradeToWriter);
			}
			finally
			{
				mainLock.ReleaseReaderLock();
			}
		}

		public TRes Read<TRes>(Func<UpgradeToWriterLockWrapper, TRes> func)
		{
			mainLock.AcquireReaderLock(600000);
			try
			{
				return func(upgradeToWriter);
			}
			finally
			{
				mainLock.ReleaseReaderLock();
			}
		}

		public void Write(Action<T> action)
		{
			mainLock.AcquireWriterLock(600000);
			try
			{
				action(value);
			}
			finally
			{
				mainLock.ReleaseWriterLock();
			}
		}

		public TRes Write<TRes>(Func<T, TRes> func)
		{
			mainLock.AcquireWriterLock(600000);
			try
			{
				return func(value);
			}
			finally
			{
				mainLock.ReleaseWriterLock();
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
			return Read(v =>
				{
					if (!writeCondition(v.Value))
						return read(v.Value);
					else
					{
						return v.Write(vw =>
							{
								if (!vw.AnyWritersSince || writeCondition(v.Value))
									return write(vw.Value);
								else
									return read(vw.Value);
							});
					}
				});
		}

		public class UpgradeToWriterLockWrapper
		{
			private ReaderWriterLockWrapper<T> readerWriterLock;

			public T Value
			{
				get
				{
					return readerWriterLock.value;
				}
			}

			public UpgradeToWriterLockWrapper(ReaderWriterLockWrapper<T> readerWriterLock)
			{
				this.readerWriterLock = readerWriterLock;
			}

			public void Write(Action<WriterLockWrapper> action)
			{
				var wlw = new WriterLockWrapper(readerWriterLock);
				var lockCookie = readerWriterLock.mainLock.UpgradeToWriterLock(600000);
				try
				{
					action(wlw);
				}
				finally
				{
					readerWriterLock.mainLock.DowngradeFromWriterLock(ref lockCookie);
				}
			}

			public TRes Write<TRes>(Func<WriterLockWrapper, TRes> func)
			{
				var wlw = new WriterLockWrapper(readerWriterLock);
				var lockCookie = readerWriterLock.mainLock.UpgradeToWriterLock(600000);
				try
				{
					return func(wlw);
				}
				finally
				{
					readerWriterLock.mainLock.DowngradeFromWriterLock(ref lockCookie);
				}
			}
		}

		public class WriterLockWrapper
		{
			private ReaderWriterLockWrapper<T> readerWriterLock;
			private int writerSeqNum;

			public T Value
			{
				get
				{
					return readerWriterLock.value;
				}
			}

			public bool AnyWritersSince
			{
				get
				{
					return readerWriterLock.mainLock.AnyWritersSince(writerSeqNum);
				}
			}

			public WriterLockWrapper(ReaderWriterLockWrapper<T> readerWriterLock)
			{
				this.readerWriterLock = readerWriterLock;
				writerSeqNum = readerWriterLock.mainLock.WriterSeqNum;
			}
		}

		public void AcquireReaderLock()
		{
			mainLock.AcquireReaderLock(600000);
		}

		public void ReleaseReaderLock()
		{
			mainLock.ReleaseReaderLock();
		}

		public void AcquireWriterLock()
		{
			mainLock.AcquireWriterLock(600000);
		}

		public void ReleaseWriterLock()
		{
			mainLock.ReleaseWriterLock();
		}
	}

	public enum LockType
	{
		Read,
		Write
	}

	public static class ReadWriteLock
	{
		public static void Lock(IEnumerable<KeyValuePair<LockType, IReadWriteLock>> readWriteLocks, Action action)
		{
			try
			{
				foreach (var rwLock in readWriteLocks)
				{
					if (rwLock.Key == LockType.Write)
						rwLock.Value.AcquireWriterLock();
					else
						rwLock.Value.AcquireReaderLock();
				}
				action();
			}
			finally
			{
				foreach (var rwLock in readWriteLocks)
				{
					if (rwLock.Key == LockType.Write)
						rwLock.Value.ReleaseWriterLock();
					else
						rwLock.Value.ReleaseReaderLock();
				}
			}
		}
	}
}
