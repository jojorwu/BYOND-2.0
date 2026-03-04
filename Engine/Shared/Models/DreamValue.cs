using Shared.Enums;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared;
    /// <summary>
    /// The fundamental data type in the Dream VM.
    /// Utilizes an explicit memory union to store 64-bit doubles and longs in the same slot,
    /// ensuring absolute precision for object IDs and bitwise operations.
    /// </summary>
    [JsonConverter(typeof(DreamValueConverter))]
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct DreamValue : IEquatable<DreamValue>
    {
        /// <summary>
        /// The runtime type tag for this value.
        /// </summary>
        [FieldOffset(0)]
        public readonly DreamValueType Type;

        /// <summary>
        /// Stores floating-point numeric data. Shares memory with _longValue.
        /// </summary>
        [FieldOffset(8)]
        private readonly double _floatValue;

        /// <summary>
        /// Stores integer numeric data (IDs, bitsets). Shares memory with _floatValue.
        /// </summary>
        [FieldOffset(8)]
        private readonly long _longValue;

        /// <summary>
        /// Reference to heap-allocated objects (strings, lists, objects).
        /// </summary>
        [FieldOffset(16)]
        private readonly object? _objectValue;

        public static readonly DreamValue Null = new DreamValue();
        public static readonly DreamValue True = new DreamValue(1.0);
        public static readonly DreamValue False = new DreamValue(0.0);

        public bool IsNull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Type == DreamValueType.Null;
        }

        public DreamValue()
        {
            _floatValue = 0;
            _longValue = 0;
            _objectValue = null;
            Type = DreamValueType.Null;
        }

        public DreamValue(double value)
        {
            _objectValue = null;
            Type = DreamValueType.Float;
            _floatValue = value;
        }

        public DreamValue(float value)
        {
            _objectValue = null;
            Type = DreamValueType.Float;
            _floatValue = value;
        }

        public DreamValue(long value)
        {
            _objectValue = null;
            Type = DreamValueType.Integer;
            _longValue = value;
        }

        public DreamValue(string value)
        {
            _longValue = 0;
            _objectValue = value ?? throw new ArgumentNullException(nameof(value));
            Type = DreamValueType.String;
        }

        private DreamValue(DreamValueType type, double floatValue, object? objectValue)
        {
            _floatValue = floatValue;
            Type = type;
            _objectValue = objectValue;
        }

        private DreamValue(DreamValueType type, long longValue, object? objectValue)
        {
            _longValue = longValue;
            Type = type;
            _objectValue = objectValue;
        }

        public static DreamValue CreateObjectIdReference(long objectId)
        {
            return new DreamValue(DreamValueType.DreamObject, objectId, null);
        }

        public DreamValue(DreamObject value)
        {
            _longValue = 0;
            Type = DreamValueType.DreamObject;
            _objectValue = value ?? throw new ArgumentNullException(nameof(value));
        }

        public DreamValue(ObjectType value)
        {
            _longValue = 0;
            Type = DreamValueType.DreamType;
            _objectValue = value ?? throw new ArgumentNullException(nameof(value));
        }

        public DreamValue(DreamResource value)
        {
            _longValue = 0;
            Type = DreamValueType.DreamResource;
            _objectValue = value ?? throw new ArgumentNullException(nameof(value));
        }

        public DreamValue(IDreamProc value)
        {
            _longValue = 0;
            Type = DreamValueType.DreamProc;
            _objectValue = value ?? throw new ArgumentNullException(nameof(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDreamProc? GetValueAsDreamProc()
        {
            if (Type == DreamValueType.DreamProc)
            {
                return (IDreamProc?)_objectValue;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(out double value)
        {
            if (Type == DreamValueType.Float)
            {
                value = _floatValue;
                return true;
            }

            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(out float value)
        {
            if (Type == DreamValueType.Float)
            {
                value = (float)_floatValue;
                return true;
            }

            value = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue([NotNullWhen(true)] out string? value)
        {
            if (Type == DreamValueType.String)
            {
                value = (string)_objectValue!;
                return true;
            }

            value = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue([NotNullWhen(true)] out DreamObject? value)
        {
            if (Type == DreamValueType.DreamObject)
            {
                value = (DreamObject)_objectValue!;
                return true;
            }

            value = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue([NotNullWhen(true)] out ObjectType? value)
        {
            if (Type == DreamValueType.DreamType)
            {
                value = (ObjectType)_objectValue!;
                return true;
            }

            value = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue([NotNullWhen(true)] out DreamResource? value)
        {
            if (Type == DreamValueType.DreamResource)
            {
                value = (DreamResource)_objectValue!;
                return true;
            }

            value = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue([NotNullWhen(true)] out IDreamProc? value)
        {
            if (Type == DreamValueType.DreamProc)
            {
                value = (IDreamProc)_objectValue!;
                return true;
            }

            value = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamObject? GetValueAsDreamObject()
        {
            if (Type == DreamValueType.DreamObject)
            {
                return (DreamObject?)_objectValue;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValueAsGameObject([NotNullWhen(true)] out GameObject? gameObject)
        {
            if (Type == DreamValueType.DreamObject && _objectValue is GameObject obj)
            {
                gameObject = obj;
                return true;
            }
            gameObject = null;
            return false;
        }

        public bool IsObjectIdReference => Type == DreamValueType.DreamObject && _objectValue == null;
        public long ObjectId => _longValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double AsDouble()
        {
            if (Type == DreamValueType.Float) return _floatValue;
            if (Type == DreamValueType.Integer) return (double)_longValue;
            throw new InvalidOperationException($"Cannot read DreamValue of type {Type} as a double");
        }

        public float AsFloat()
        {
            if (Type == DreamValueType.Float) return (float)_floatValue;
            if (Type == DreamValueType.Integer) return (float)_longValue;
            throw new InvalidOperationException($"Cannot read DreamValue of type {Type} as a float");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AsInt()
        {
            if (Type == DreamValueType.Float) return (int)_floatValue;
            if (Type == DreamValueType.Integer) return (int)_longValue;
            throw new InvalidOperationException($"Cannot read DreamValue of type {Type} as an int");
        }

        /// <summary>
        /// Retrieves the bit-level 64-bit value regardless of the numeric tag.
        /// Safe because Float and Integer share the same 8-byte offset.
        /// </summary>
        public double RawDouble
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Type == DreamValueType.Float) ? _floatValue : (double)_longValue;
        }

        /// <summary>
        /// Retrieves the raw 64-bit integer bits regardless of the numeric tag.
        /// Useful for bitwise operations and exact ID comparisons.
        /// </summary>
        public long RawLong
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Type == DreamValueType.Float) ? (long)_floatValue : _longValue;
        }

        public float RawFloat
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (float)RawDouble;
        }

        public static implicit operator DreamValue(double value) => new DreamValue(value);
        public static implicit operator DreamValue(float value) => new DreamValue(value);
        public static implicit operator DreamValue(int value) => new DreamValue((double)value);
        public static implicit operator DreamValue(string value) => new DreamValue(value);
        public static implicit operator DreamValue(DreamObject value) => new DreamValue(value);
        public static implicit operator DreamValue(ObjectType value) => new DreamValue(value);
        public static implicit operator DreamValue(DreamResource value) => new DreamValue(value);

        public static DreamValue FromObject(object? value)
        {
            if (value is DreamValue dv) return dv;
            return value switch
            {
                null => Null,
                string s => new DreamValue(s),
                int i => new DreamValue((long)i),
                long l => new DreamValue(l),
                float f => new DreamValue(f),
                double d => new DreamValue(d),
                DreamObject o => new DreamValue(o),
                ObjectType t => new DreamValue(t),
                DreamResource r => new DreamValue(r),
                IDreamProc p => new DreamValue(p),
                JsonElement e => FromJsonElement(e),
                _ => throw new ArgumentException($"Unsupported type for DreamValue: {value.GetType()}")
            };
        }

        private static DreamValue FromJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    return new DreamValue(element.GetSingle());
                case JsonValueKind.String:
                    return new DreamValue(element.GetString()!);
                case JsonValueKind.Null:
                    return Null;
                default:
                    // For complex types, we might want to store them as strings or handle them specifically
                    return new DreamValue(element.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return Type switch
            {
                DreamValueType.Float => _floatValue.ToString(CultureInfo.InvariantCulture),
                DreamValueType.Integer => _longValue.ToString(CultureInfo.InvariantCulture),
                DreamValueType.String => (string)_objectValue! ?? string.Empty,
                DreamValueType.DreamObject => _objectValue?.ToString() ?? string.Empty,
                DreamValueType.Null => string.Empty,
                DreamValueType.DreamType => ((ObjectType)_objectValue!).Name,
                _ => _objectValue?.ToString() ?? string.Empty
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetValueAsDouble()
        {
            return Type switch
            {
                DreamValueType.Float => _floatValue,
                DreamValueType.Integer => (double)_longValue,
                DreamValueType.Null => 0.0,
                _ => (double)_longValue // Optimized fallback to _longValue for other types
            };
        }

        public float GetValueAsFloat()
        {
            return (float)GetValueAsDouble();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator +(DreamValue a, DreamValue b)
        {
            // Hot path: numeric
            if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
            {
                if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                    return new DreamValue(a._longValue + b._longValue);
                return new DreamValue(a.RawDouble + b.RawDouble);
            }

            // String concatenation
            if (a.Type == DreamValueType.String || b.Type == DreamValueType.String)
            {
                var sA = a.ToString();
                var sB = b.ToString();

                if ((long)sA.Length + sB.Length > 1073741824)
                    throw new System.InvalidOperationException("Maximum string length exceeded during concatenation");

                return new DreamValue(sA + sB);
            }

            // List addition
            if (a.Type == DreamValueType.DreamObject && a._objectValue is DreamList listA)
            {
                var newList = listA.Clone();
                if (b.Type == DreamValueType.DreamObject && b._objectValue is DreamList listB)
                {
                    foreach (var val in listB.Values) newList.AddValue(val);
                }
                else
                {
                    newList.AddValue(b);
                }
                return new DreamValue(newList);
            }

            // Fallback to math
            return new DreamValue(a.GetValueAsDouble() + b.GetValueAsDouble());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator -(DreamValue a, DreamValue b)
        {
            // Hot path: numeric
            if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
            {
                if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                    return new DreamValue(a._longValue - b._longValue);
                return new DreamValue(a.RawDouble - b.RawDouble);
            }

            // List subtraction
            if (a.Type == DreamValueType.DreamObject && a._objectValue is DreamList listA)
            {
                var newList = listA.Clone();
                if (b.Type == DreamValueType.DreamObject && b._objectValue is DreamList listB)
                {
                    foreach (var val in listB.Values) newList.RemoveAll(val);
                }
                else
                {
                    newList.RemoveAll(b);
                }
                return new DreamValue(newList);
            }

            // Fallback to math
            return new DreamValue(a.GetValueAsDouble() - b.GetValueAsDouble());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator *(DreamValue a, DreamValue b)
        {
            if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
            {
                if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                    return new DreamValue(a._longValue * b._longValue);
                return new DreamValue(a.RawDouble * b.RawDouble);
            }
            return new DreamValue(a.GetValueAsDouble() * b.GetValueAsDouble());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator /(DreamValue a, DreamValue b)
        {
            double doubleB = b.Type == DreamValueType.Float ? b._floatValue : b.GetValueAsDouble();
            if (doubleB == 0)
                return new DreamValue(0.0);

            double doubleA = a.Type == DreamValueType.Float ? a._floatValue : a.GetValueAsDouble();
            return new DreamValue(doubleA / doubleB);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator %(DreamValue a, DreamValue b)
        {
            double doubleB = b.Type == DreamValueType.Float ? b._floatValue : b.GetValueAsDouble();
            if (doubleB == 0)
                return new DreamValue(0.0);

            double doubleA = a.Type == DreamValueType.Float ? a._floatValue : a.GetValueAsDouble();
            return new DreamValue(doubleA % doubleB);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
        {
            return obj is DreamValue other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DreamValue other)
        {
            if (Type != other.Type) return false;

            if (Type == DreamValueType.Float)
                return BitConverter.DoubleToInt64Bits(_floatValue) == BitConverter.DoubleToInt64Bits(other._floatValue);

            if (Type == DreamValueType.Integer)
                return _longValue == other._longValue;

            return ReferenceEquals(_objectValue, other._objectValue) || (_objectValue != null && _objectValue.Equals(other._objectValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            // Use bit-level representation for floats to avoid fuzzy hash issues in collections
            // while maintaining speed.
            if (Type == DreamValueType.Float) return _floatValue.GetHashCode();
            if (Type == DreamValueType.Integer) return _longValue.GetHashCode();
            if (Type == DreamValueType.Null) return 0;

            return HashCode.Combine(Type, _objectValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(DreamValue a, DreamValue b)
        {
            if (a.Type == b.Type)
            {
                if (a.Type == DreamValueType.Float)
                    return a._floatValue == b._floatValue || Math.Abs(a._floatValue - b._floatValue) < 0.00001;
                return a.Equals(b);
            }

            // DM Parity: null == 0
            if (a.Type == DreamValueType.Null)
            {
                if (b.Type == DreamValueType.Float) return b._floatValue == 0 || Math.Abs(b._floatValue) < 0.00001;
                if (b.Type == DreamValueType.Integer) return b._longValue == 0;
            }
            if (b.Type == DreamValueType.Null)
            {
                if (a.Type == DreamValueType.Float) return a._floatValue == 0 || Math.Abs(a._floatValue) < 0.00001;
                if (a.Type == DreamValueType.Integer) return a._longValue == 0;
            }

            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Integer)
                return a._floatValue == b._longValue || Math.Abs(a._floatValue - b._longValue) < 0.00001;
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Float)
                return a._longValue == b._floatValue || Math.Abs(a._longValue - b._floatValue) < 0.00001;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(DreamValue a, DreamValue b)
        {
            return !(a == b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return a._floatValue > b._floatValue;
            if (a.Type == DreamValueType.String && b.Type == DreamValueType.String)
                return string.CompareOrdinal((string)a._objectValue!, (string)b._objectValue!) > 0;
            return a.GetValueAsDouble() > b.GetValueAsDouble();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return a._floatValue < b._floatValue;
            if (a.Type == DreamValueType.String && b.Type == DreamValueType.String)
                return string.CompareOrdinal((string)a._objectValue!, (string)b._objectValue!) < 0;
            return a.GetValueAsDouble() < b.GetValueAsDouble();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return a._floatValue >= b._floatValue;
            if (a.Type == DreamValueType.String && b.Type == DreamValueType.String)
                return string.CompareOrdinal((string)a._objectValue!, (string)b._objectValue!) >= 0;
            return a.GetValueAsDouble() >= b.GetValueAsDouble();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return a._floatValue <= b._floatValue;
            if (a.Type == DreamValueType.String && b.Type == DreamValueType.String)
                return string.CompareOrdinal((string)a._objectValue!, (string)b._objectValue!) <= 0;
            return a.GetValueAsDouble() <= b.GetValueAsDouble();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator -(DreamValue a)
        {
            return new DreamValue(-a.GetValueAsDouble());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator !(DreamValue a)
        {
            return new DreamValue(a.IsFalse() ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator &(DreamValue a, DreamValue b)
        {
            return new DreamValue(a.RawLong & b.RawLong);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator |(DreamValue a, DreamValue b)
        {
            return new DreamValue(a.RawLong | b.RawLong);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator ^(DreamValue a, DreamValue b)
        {
            return new DreamValue(a.RawLong ^ b.RawLong);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator ~(DreamValue a)
        {
            return new DreamValue(~a.RawLong);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator <<(DreamValue a, DreamValue b)
        {
            return new DreamValue(SharedOperations.BitShiftLeft(a.RawLong, b.RawLong));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator >>(DreamValue a, DreamValue b)
        {
            return new DreamValue(SharedOperations.BitShiftRight(a.RawLong, b.RawLong));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFalse()
        {
            switch (Type)
            {
                case DreamValueType.Null:
                    return true;
                case DreamValueType.Float:
                    return _floatValue == 0.0;
                case DreamValueType.Integer:
                    return _longValue == 0;
                case DreamValueType.String:
                    return ((string)_objectValue!).Length == 0;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(Utf8JsonWriter writer, JsonSerializerOptions options)
        {
            switch (Type)
            {
                case DreamValueType.Float:
                    writer.WriteNumberValue(_floatValue);
                    break;
                case DreamValueType.Integer:
                    writer.WriteNumberValue(_longValue);
                    break;
                case DreamValueType.String:
                    writer.WriteStringValue((string)_objectValue!);
                    break;
                case DreamValueType.Null:
                    writer.WriteNullValue();
                    break;
                default:
                    writer.WriteStringValue(ToString());
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetWriteSize()
        {
            int size = 1; // Type byte
            switch (Type)
            {
                case DreamValueType.Float:
                    size += 8;
                    break;
                case DreamValueType.Integer:
                    size += 8;
                    break;
                case DreamValueType.String:
                    {
                        int len = System.Text.Encoding.UTF8.GetByteCount((string)_objectValue!);
                        size += GetVarIntSize(len) + len;
                    }
                    break;
                case DreamValueType.Null:
                    break;
                case DreamValueType.DreamObject:
                    size += 1; // Boolean flag
                    if (_objectValue is GameObject) size += 8; // ID
                    else
                    {
                        int len = System.Text.Encoding.UTF8.GetByteCount(ToString());
                        size += GetVarIntSize(len) + len;
                    }
                    break;
                default:
                    {
                        int len = System.Text.Encoding.UTF8.GetByteCount(ToString());
                        size += GetVarIntSize(len) + len;
                    }
                    break;
            }
            return size;
        }

        private static int GetVarIntSize(long value)
        {
            ulong v = (ulong)value;
            int count = 1;
            while (v >= 0x80)
            {
                v >>= 7;
                count++;
            }
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteTo(Span<byte> span)
        {
            span[0] = (byte)Type;
            int offset = 1;
            switch (Type)
            {
                case DreamValueType.Float:
                    System.Buffers.Binary.BinaryPrimitives.WriteDoubleLittleEndian(span.Slice(offset), _floatValue);
                    return offset + 8;
                case DreamValueType.Integer:
                    System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(span.Slice(offset), _longValue);
                    return offset + 8;
                case DreamValueType.String:
                    {
                        var s = (string)_objectValue!;
                        int bytesWritten = System.Text.Encoding.UTF8.GetByteCount(s);
                        // VarInt length prefix
                        int lenBytes = WriteVarInt(span.Slice(offset), bytesWritten);
                        offset += lenBytes;
                        System.Text.Encoding.UTF8.GetBytes(s, span.Slice(offset));
                        return offset + bytesWritten;
                    }
                case DreamValueType.Null:
                    return offset;
                case DreamValueType.DreamObject:
                    if (_objectValue is GameObject g)
                    {
                        span[offset++] = 1; // Boolean true
                        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(span.Slice(offset), g.Id);
                        return offset + 8;
                    }
                    else
                    {
                        span[offset++] = 0; // Boolean false
                        var s = ToString();
                        int bytesWritten = System.Text.Encoding.UTF8.GetByteCount(s);
                        int lenBytes = WriteVarInt(span.Slice(offset), bytesWritten);
                        offset += lenBytes;
                        System.Text.Encoding.UTF8.GetBytes(s, span.Slice(offset));
                        return offset + bytesWritten;
                    }
                default:
                    {
                        var s = ToString();
                        int bytesWritten = System.Text.Encoding.UTF8.GetByteCount(s);
                        int lenBytes = WriteVarInt(span.Slice(offset), bytesWritten);
                        offset += lenBytes;
                        System.Text.Encoding.UTF8.GetBytes(s, span.Slice(offset));
                        return offset + bytesWritten;
                    }
            }
        }

        private static int WriteVarInt(Span<byte> span, long value)
        {
            ulong v = (ulong)value;
            int count = 0;
            while (v >= 0x80)
            {
                span[count++] = (byte)(v | 0x80);
                v >>= 7;
            }
            span[count++] = (byte)v;
            return count;
        }

        public static DreamValue ReadFrom(ReadOnlySpan<byte> span, out int bytesRead)
        {
            var type = (DreamValueType)span[0];
            int offset = 1;
            switch (type)
            {
                case DreamValueType.Float:
                    bytesRead = offset + 8;
                    return new DreamValue(System.Buffers.Binary.BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(offset)));
                case DreamValueType.Integer:
                    bytesRead = offset + 8;
                    return new DreamValue(System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset)));
                case DreamValueType.String:
                    {
                        long len = ReadVarInt(span.Slice(offset), out int lenBytes);
                        offset += lenBytes;
                        bytesRead = offset + (int)len;
                        return new DreamValue(System.Text.Encoding.UTF8.GetString(span.Slice(offset, (int)len)));
                    }
                case DreamValueType.Null:
                    bytesRead = offset;
                    return Null;
                case DreamValueType.DreamObject:
                    if (span[offset++] != 0)
                    {
                        bytesRead = offset + 8;
                        return CreateObjectIdReference(System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset)));
                    }
                    else
                    {
                        long len = ReadVarInt(span.Slice(offset), out int lenBytes);
                        offset += lenBytes;
                        bytesRead = offset + (int)len;
                        return new DreamValue(System.Text.Encoding.UTF8.GetString(span.Slice(offset, (int)len)));
                    }
                default:
                    {
                        long len = ReadVarInt(span.Slice(offset), out int lenBytes);
                        offset += lenBytes;
                        bytesRead = offset + (int)len;
                        return new DreamValue(System.Text.Encoding.UTF8.GetString(span.Slice(offset, (int)len)));
                    }
            }
        }

        private static long ReadVarInt(ReadOnlySpan<byte> span, out int bytesRead)
        {
            long result = 0;
            int shift = 0;
            bytesRead = 0;
            while (true)
            {
                byte b = span[bytesRead++];
                result |= (long)(b & 0x7f) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
            }
        }
    }

    public class DreamValueConverter : JsonConverter<DreamValue>
    {
        public override DreamValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return new DreamValue(reader.GetDouble());
                case JsonTokenType.String:
                    return new DreamValue(reader.GetString()!);
                case JsonTokenType.True:
                    return new DreamValue(1.0);
                case JsonTokenType.False:
                    return new DreamValue(0.0);
                case JsonTokenType.Null:
                    return DreamValue.Null;
                default:
                    // For complex objects, we might need more logic
                    using (var doc = JsonDocument.ParseValue(ref reader))
                    {
                        return DreamValue.FromObject(doc.RootElement.Clone());
                    }
            }
        }

        public override void Write(Utf8JsonWriter writer, DreamValue value, JsonSerializerOptions options)
        {
            value.WriteTo(writer, options);
        }
    }
