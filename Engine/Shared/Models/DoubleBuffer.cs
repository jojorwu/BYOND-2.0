using System;

namespace Shared.Models;
    /// <summary>
    /// Managed double-buffer for thread-safe state reading and writing.
    /// </summary>
    /// <typeparam name="T">The type of the state data.</typeparam>
    public class DoubleBuffer<T> where T : class
    {
        private T _read;
        private T _write;

        public T Read => _read;
        public T Write => _write;

        public DoubleBuffer(T initialState)
        {
            _read = initialState ?? throw new ArgumentNullException(nameof(initialState));
            _write = initialState; // Initially both point to same, but should be separated if mutable
        }

        public DoubleBuffer(T readState, T writeState)
        {
            _read = readState ?? throw new ArgumentNullException(nameof(readState));
            _write = writeState ?? throw new ArgumentNullException(nameof(writeState));
        }

        /// <summary>
        /// Swaps the read and write buffers.
        /// </summary>
        public void Swap()
        {
            var temp = _read;
            _read = _write;
            _write = temp;
        }

        /// <summary>
        /// Copies data from one buffer to another (if deep copy is needed) before swapping.
        /// </summary>
        public void Commit(Action<T, T> copyFunc)
        {
            copyFunc(_read, _write);
            Swap();
        }
    }
