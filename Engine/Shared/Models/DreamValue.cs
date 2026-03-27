using Shared.Enums;
using Shared.Utils;
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

        public string StringValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ToString();
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
        /// Retrieves the numeric data as a 64-bit double, performing conversion if the tag is an Integer.
        /// This is safe because both numeric types share the same 8-byte memory offset at FieldOffset(8).
        /// </summary>
        /// <returns>The double-precision floating-point representation of the numeric value.</returns>
        public double RawDouble
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Type == DreamValueType.Float) ? _floatValue : (double)_longValue;
        }

        /// <summary>
        /// Highly optimized, zero-branch access to the floating-point field.
        /// <para>Warning: You must ensure the Type is <see cref="DreamValueType.Float"/> before calling this to avoid interpreting raw integer bits as a double.</para>
        /// </summary>
        /// <returns>The raw double value stored in the slot.</returns>
        public double UnsafeRawDouble
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _floatValue;
        }

        /// <summary>
        /// Retrieves the numeric data as a 64-bit long integer, performing conversion if the tag is a Float.
        /// Useful for bitwise operations on values that might be stored as doubles.
        /// </summary>
        /// <returns>The signed 64-bit integer representation of the numeric value.</returns>
        public long RawLong
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Type == DreamValueType.Float) ? (long)_floatValue : _longValue;
        }

        /// <summary>
        /// Highly optimized, zero-branch access to the integer data field.
        /// <para>Warning: You must ensure the Type is <see cref="DreamValueType.Integer"/> or another ID-based type before calling this.</para>
        /// </summary>
        /// <returns>The raw 64-bit integer value stored in the slot.</returns>
        public long UnsafeRawLong
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _longValue;
        }

        /// <summary>
        /// Highly optimized, zero-branch access to the object reference field.
        /// <para>Warning: Use only after verifying the Type tag implies an object reference.</para>
        /// </summary>
        /// <returns>The raw object reference stored in the heap-value slot.</returns>
        public object? UnsafeRawObject
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _objectValue;
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
                DreamValueType.String => Unsafe.As<object?, string>(ref Unsafe.AsRef(in _objectValue)) ?? string.Empty,
                DreamValueType.DreamObject => _objectValue?.ToString() ?? string.Empty,
                DreamValueType.Null => string.Empty,
                DreamValueType.DreamType => Unsafe.As<object?, ObjectType>(ref Unsafe.AsRef(in _objectValue)).Name,
                _ => _objectValue?.ToString() ?? string.Empty
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendTo(System.Text.StringBuilder sb)
        {
            switch (Type)
            {
                case DreamValueType.Float:
                    sb.Append(_floatValue);
                    break;
                case DreamValueType.Integer:
                    sb.Append(_longValue);
                    break;
                case DreamValueType.String:
                    sb.Append((string)_objectValue!);
                    break;
                case DreamValueType.Null:
                    break;
                case DreamValueType.DreamType:
                    sb.Append(((ObjectType)_objectValue!).Name);
                    break;
                default:
                    sb.Append(_objectValue?.ToString());
                    break;
            }
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

                double da = a.Type == DreamValueType.Float ? a._floatValue : (double)a._longValue;
                double db = b.Type == DreamValueType.Float ? b._floatValue : (double)b._longValue;
                return new DreamValue(da + db);
            }

            // String concatenation
            if (a.Type == DreamValueType.String || b.Type == DreamValueType.String)
            {
                var sb = _concatStringBuilder.Value!;
                sb.Clear();
                a.AppendTo(sb);
                b.AppendTo(sb);

                if (sb.Length > 1073741824)
                    throw new System.InvalidOperationException("Maximum string length exceeded during concatenation");

                return new DreamValue(sb.ToString());
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

                double da = a.Type == DreamValueType.Float ? a._floatValue : (double)a._longValue;
                double db = b.Type == DreamValueType.Float ? b._floatValue : (double)b._longValue;
                return new DreamValue(da - db);
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

                double da = a.Type == DreamValueType.Float ? a._floatValue : (double)a._longValue;
                double db = b.Type == DreamValueType.Float ? b._floatValue : (double)b._longValue;
                return new DreamValue(da * db);
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

            if (Type <= DreamValueType.Integer)
                return _longValue == other._longValue;

            return ReferenceEquals(_objectValue, other._objectValue) || (_objectValue != null && _objectValue.Equals(other._objectValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            // Use bit-level representation for both numeric types (they share the same offset).
            // This is safe because DreamValueType ensures we don't alias across types.
            if (Type <= DreamValueType.Integer) return _longValue.GetHashCode();
            if (Type == DreamValueType.Null) return 0;

            return HashCode.Combine(Type, _objectValue);
        }

        [ThreadStatic]
        private static System.Text.StringBuilder? _concatStringBuilderInstance;
        private static ThreadLocal<System.Text.StringBuilder> _concatStringBuilder = new(() => _concatStringBuilderInstance ??= new System.Text.StringBuilder(128));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(DreamValue a, DreamValue b)
        {
            // Hot path: identical memory layout (same type and data/reference)
            if (a.Type == b.Type)
            {
                if (a.Type <= DreamValueType.Integer)
                {
                    // Integers must match exactly, floats use an epsilon for DM parity
                    if (a.Type == DreamValueType.Integer) return a._longValue == b._longValue;
                    return a._floatValue == b._floatValue || Math.Abs(a._floatValue - b._floatValue) < 1e-5;
                }
                return ReferenceEquals(a._objectValue, b._objectValue);
            }

            // Mixed numeric equality (Integer <-> Float)
            if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
            {
                double da = a.Type == DreamValueType.Float ? a._floatValue : (double)a._longValue;
                double db = b.Type == DreamValueType.Float ? b._floatValue : (double)b._longValue;
                return da == db || Math.Abs(da - db) < 1e-5;
            }

            // DM Parity: null == 0
            if (a.Type == DreamValueType.Null)
            {
                if (b.Type <= DreamValueType.Integer)
                {
                    if (b.Type == DreamValueType.Integer) return b._longValue == 0;
                    return b._floatValue == 0 || Math.Abs(b._floatValue) < 1e-5;
                }
                return false;
            }
            if (b.Type == DreamValueType.Null)
            {
                if (a.Type <= DreamValueType.Integer)
                {
                    if (a.Type == DreamValueType.Integer) return a._longValue == 0;
                    return a._floatValue == 0 || Math.Abs(a._floatValue) < 1e-5;
                }
                return false;
            }

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
            // Optimized truthiness check: identify Null or 0 states in a single operation
            // by checking if both the data slot and object reference are zero.
            // These states (Null, Float 0.0, Integer 0) all have a zero bit pattern in these slots.
            if ((_longValue | (long)Unsafe.As<object?, IntPtr>(ref Unsafe.AsRef(in _objectValue))) == 0)
            {
                // We check the type tag to distinguish from DreamObject with ID 0 (which is True).
                // Null (3), Float (0), and Integer (1) are all <= DreamValueType.Null (3).
                return Type <= DreamValueType.Null;
            }

            // The only other False value in DM is the empty string.
            return Type == DreamValueType.String && Unsafe.As<object?, string>(ref Unsafe.AsRef(in _objectValue)).Length == 0;
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
                        size += Utils.VarInt.GetSize(len) + len;
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
                        size += Utils.VarInt.GetSize(len) + len;
                    }
                    break;
                default:
                    {
                        int len = System.Text.Encoding.UTF8.GetByteCount(ToString());
                        size += Utils.VarInt.GetSize(len) + len;
                    }
                    break;
            }
            return size;
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
                        int lenBytes = Utils.VarInt.Write(span.Slice(offset), bytesWritten);
                        offset += lenBytes;
                        System.Text.Encoding.UTF8.GetBytes(s, span.Slice(offset));
                        return offset + bytesWritten;
                    }
                case DreamValueType.Null:
                    return offset;
                case DreamValueType.DreamObject:
                    if (_objectValue is GameObject g)
                    {
                        span[offset++] = 1; // Object ID flag
                        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(span.Slice(offset), g.Id);
                        return offset + 8;
                    }
                    else
                    {
                        span[offset++] = 0; // String-based ref flag
                        var s = (_objectValue != null) ? _objectValue.ToString() ?? string.Empty : string.Empty;
                        int bytesWritten = System.Text.Encoding.UTF8.GetByteCount(s);
                        int lenBytes = Utils.VarInt.Write(span.Slice(offset), bytesWritten);
                        offset += lenBytes;
                        System.Text.Encoding.UTF8.GetBytes(s, span.Slice(offset));
                        return offset + bytesWritten;
                    }
                default:
                    {
                        var s = ToString();
                        int bytesWritten = System.Text.Encoding.UTF8.GetByteCount(s);
                        int lenBytes = Utils.VarInt.Write(span.Slice(offset), bytesWritten);
                        offset += lenBytes;
                        System.Text.Encoding.UTF8.GetBytes(s, span.Slice(offset));
                        return offset + bytesWritten;
                    }
            }
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
                        long len = Utils.VarInt.Read(span.Slice(offset), out int lenBytes);
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
                        long len = Utils.VarInt.Read(span.Slice(offset), out int lenBytes);
                        offset += lenBytes;
                        bytesRead = offset + (int)len;
                        return new DreamValue(System.Text.Encoding.UTF8.GetString(span.Slice(offset, (int)len)));
                    }
                default:
                    {
                        long len = Utils.VarInt.Read(span.Slice(offset), out int lenBytes);
                        offset += lenBytes;
                        bytesRead = offset + (int)len;
                        return new DreamValue(System.Text.Encoding.UTF8.GetString(span.Slice(offset, (int)len)));
                    }
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
