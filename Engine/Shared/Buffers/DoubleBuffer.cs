using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using Shared.Interfaces;

namespace Shared.Buffers;
    /// <summary>
    /// Managed double-buffer for thread-safe state reading and writing.
    /// </summary>
    /// <typeparam name="T">The type of the state data.</typeparam>
    public class DoubleBuffer<T> : IBuffer where T : class
    {
        private T _read;
        private T _write;
        private readonly IDiagnosticBus? _diagnosticBus;

        public T Read => Volatile.Read(ref _read);
        public T Write => Volatile.Read(ref _write);

        /// <inheritdoc />
        public long Capacity => 2;

        /// <inheritdoc />
        public long Position => 0;

        /// <inheritdoc />
        public long Length => 0;

        /// <inheritdoc />
        public ReadOnlySequence<byte> WrittenSequence => ReadOnlySequence<byte>.Empty;

        /// <inheritdoc />
        public int SlabCount => 2;

        /// <inheritdoc />
        public long TotalAllocatedBytes => 0; // Managed objects, size unknown

        /// <inheritdoc />
        public bool IsPinned => false;

        public DoubleBuffer(T initialState, IDiagnosticBus? diagnosticBus = null)
        {
            _read = initialState ?? throw new ArgumentNullException(nameof(initialState));
            _write = initialState; // Initially both point to same, but should be separated if mutable
            _diagnosticBus = diagnosticBus;
        }

        public DoubleBuffer(T readState, T writeState, IDiagnosticBus? diagnosticBus = null)
        {
            _read = readState ?? throw new ArgumentNullException(nameof(readState));
            _write = writeState ?? throw new ArgumentNullException(nameof(writeState));
            _diagnosticBus = diagnosticBus;
        }

        /// <summary>
        /// Swaps the read and write buffers. This operation is thread-safe.
        /// </summary>
        public void Swap()
        {
            lock (this)
            {
                var temp = _read;
                _read = _write;
                _write = temp;
            }
        }

        /// <summary>
        /// Copies data from one buffer to another (if deep copy is needed) before swapping.
        /// </summary>
        public void Commit(Action<T, T> copyFunc)
        {
            copyFunc(_read, _write);
            Swap();
        }

        /// <inheritdoc />
        public void Reset() { }

        /// <inheritdoc />
        public void CopyTo(System.IO.Stream destination) => throw new NotSupportedException();

        /// <inheritdoc />
        public void CopyTo(Span<byte> destination) => throw new NotSupportedException();

        /// <inheritdoc />
        public ReadOnlySpan<byte> GetSegmentAsSpan(long offset, int length) => throw new NotSupportedException();

        /// <inheritdoc />
        public Span<byte> GetMutableSegmentAsSpan(long offset, int length) => throw new NotSupportedException();

        /// <inheritdoc />
        public void Shrink() { }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object> GetDiagnosticInfo()
        {
            var info = new Dictionary<string, object>
            {
                ["Type"] = typeof(T).Name,
                ["SlabCount"] = SlabCount
            };

            _diagnosticBus?.Publish("Buffer", $"DoubleBuffer<{typeof(T).Name}> Stats", info, (m, state) =>
            {
                foreach (var kvp in state) m.Add(kvp.Key, kvp.Value.ToString() ?? string.Empty);
            });

            return info;
        }
    }
