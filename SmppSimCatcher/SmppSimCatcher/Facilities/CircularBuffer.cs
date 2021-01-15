using HermaFx;
using System;
using System.Collections.Generic;

namespace SmppSimCatcher.Facilities
{
	public class CircularBuffer<T> : IEnumerable<T>
	{
		#region Fields
		readonly int _capacity;
		readonly object _locker;

		int _count;
		int _head;
		int _tail;
		T[] _buffer;
		#endregion

		public CircularBuffer(int max)
		{
			Guard.Against<ArgumentOutOfRangeException>(max < 0, "max < 0");

			this._capacity = max;
			_locker = new object();
			_buffer = new T[_capacity];

			Reset();
		}

		public int Size { get { return _capacity; } }
		public object SyncRoot { get { return _locker; } }

		#region Count
		public int Count { get { return UnsafeCount; } }
		public int SafeCount { get { lock (_locker) { return UnsafeCount; } } }
		public int UnsafeCount { get { return _count; } }
		#endregion

		#region Private/Helper Methods
		private static int Incr(int index, int size)
		{
			return (index + 1) % size;
		}

		private void UnsafeEnsureQueueNotEmpty()
		{
			if (_count == 0)
				throw new InvalidOperationException("Empty queue");
		}
		#endregion

		#region Reset
		public void Reset()
		{
			_count = 0;
			_head = 0;
			_tail = 0;
		}
		#endregion

		#region Enqueue

		public void Enqueue(T obj)
		{
			UnsafeEnqueue(obj);
		}

		public void SafeEnqueue(T obj)
		{
			lock (_locker) { UnsafeEnqueue(obj); }
		}

		public void UnsafeEnqueue(T obj)
		{
			_buffer[_tail] = obj;

			if (Count == Size)
				_head = Incr(_head, Size);
			_tail = Incr(_tail, Size);
			_count = Math.Min(_count + 1, Size);
		}

		#endregion

		#region Dequeue

		public T Dequeue()
		{
			return UnsafeDequeue();
		}

		public T SafeDequeue()
		{
			lock (_locker) { return UnsafeDequeue(); }
		}

		public T UnsafeDequeue()
		{
			UnsafeEnsureQueueNotEmpty();

			T res = _buffer[_head];
			_buffer[_head] = default(T);
			_head = Incr(_head, Size);
			_count--;

			return res;
		}

		#endregion

		#region Peek

		public T Peek()
		{
			return UnsafePeek();
		}

		public T SafePeek()
		{
			lock (_locker) { return UnsafePeek(); }
		}

		public T UnsafePeek()
		{
			UnsafeEnsureQueueNotEmpty();

			return _buffer[_head];
		}

		#endregion

		#region ToArray
		public T[] ToArray()
		{
			var dst = new T[_count];
			CopyTo(dst);
			return dst;
		}
		#endregion

		#region CopyTo
		public int CopyTo(T[] array)
		{
			return CopyTo(array, 0);
		}

		public int CopyTo(T[] array, int arrayIndex)
		{
			return CopyTo(array, arrayIndex, 0, array.Length);
		}

		public int CopyTo(T[] array, int arrayIndex, int index, int count)
		{
			if (count > _capacity || count > _count)
				throw new ArgumentOutOfRangeException("count");

			int i, bufferIndex = _head, max = Math.Min(count, _count);
			for (i = 0; i < max; i++, bufferIndex++, arrayIndex++)
			{
				if (bufferIndex == _capacity)
					bufferIndex = 0;
				array[arrayIndex] = _buffer[bufferIndex];
			}

			return i;
		}
		#endregion

		#region GetEnumerator

		public IEnumerator<T> GetEnumerator()
		{
			return UnsafeGetEnumerator();
		}

		public IEnumerator<T> SafeGetEnumerator()
		{
			lock (_locker)
			{
				List<T> res = new List<T>(_count);
				var enumerator = UnsafeGetEnumerator();
				while (enumerator.MoveNext())
					res.Add(enumerator.Current);
				return res.GetEnumerator();
			}
		}

		public IEnumerator<T> UnsafeGetEnumerator()
		{
			int index = _head;
			for (int i = 0; i < _count; i++)
			{
				yield return _buffer[index];
				index = Incr(index, _capacity);
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		#endregion
	}
}
