using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared
{
    [JsonConverter(typeof(DreamValueConverter))]
    public readonly struct DreamValue : IEquatable<DreamValue>
    {
        public readonly DreamValueType Type;
        private readonly float _floatValue;
        private readonly object? _objectValue;

        public static readonly DreamValue Null = new DreamValue();
        public static readonly DreamValue True = new DreamValue(1.0f);
        public static readonly DreamValue False = new DreamValue(0.0f);

        public bool IsNull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Type == DreamValueType.Null;
        }

        public DreamValue()
        {
            Type = DreamValueType.Null;
            _floatValue = 0;
            _objectValue = null;
        }

        public DreamValue(float value)
        {
            Type = DreamValueType.Float;
            _floatValue = value;
            _objectValue = null;
        }

        public DreamValue(string value)
        {
            Type = DreamValueType.String;
            _floatValue = 0;
            _objectValue = value ?? throw new ArgumentNullException(nameof(value));
        }

        public DreamValue(DreamObject value)
        {
            Type = DreamValueType.DreamObject;
            _floatValue = 0;
            _objectValue = value ?? throw new ArgumentNullException(nameof(value));
        }

        public DreamValue(ObjectType value)
        {
            Type = DreamValueType.DreamType;
            _floatValue = 0;
            _objectValue = value ?? throw new ArgumentNullException(nameof(value));
        }

        public DreamValue(DreamResource value)
        {
            Type = DreamValueType.DreamResource;
            _floatValue = 0;
            _objectValue = value ?? throw new ArgumentNullException(nameof(value));
        }

        public DreamValue(IDreamProc value)
        {
            Type = DreamValueType.DreamProc;
            _floatValue = 0;
            _objectValue = value ?? throw new ArgumentNullException(nameof(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(out float value)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float AsFloat()
        {
            if (Type != DreamValueType.Float)
                throw new InvalidOperationException($"Cannot read DreamValue of type {Type} as a float");
            return _floatValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AsInt()
        {
            if (Type != DreamValueType.Float)
                throw new InvalidOperationException($"Cannot read DreamValue of type {Type} as an int");
            return (int)_floatValue;
        }

        public float RawFloat
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _floatValue;
        }

        public static implicit operator DreamValue(float value) => new DreamValue(value);
        public static implicit operator DreamValue(int value) => new DreamValue((float)value);
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
                int i => new DreamValue(i),
                float f => new DreamValue(f),
                double d => new DreamValue((float)d),
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
                DreamValueType.String => (string)_objectValue! ?? string.Empty,
                DreamValueType.DreamObject => _objectValue?.ToString() ?? string.Empty,
                DreamValueType.Null => string.Empty,
                DreamValueType.DreamType => ((ObjectType)_objectValue!).Name,
                _ => _objectValue?.ToString() ?? string.Empty
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetValueAsFloat()
        {
            return Type switch
            {
                DreamValueType.Float => _floatValue,
                DreamValueType.Null => 0f,
                _ => 0f // Optimized fallback to 0 for non-math types to avoid checks in operators
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator +(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return new DreamValue(a._floatValue + b._floatValue);

            if (a.Type == DreamValueType.String || b.Type == DreamValueType.String)
            {
                var sA = a.ToString();
                var sB = b.ToString();

                if ((long)sA.Length + sB.Length > 67108864)
                    throw new System.InvalidOperationException("Maximum string length exceeded during concatenation");

                return new DreamValue(sA + sB);
            }

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

            return new DreamValue(a.GetValueAsFloat() + b.GetValueAsFloat());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator -(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return new DreamValue(a._floatValue - b._floatValue);

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
            return new DreamValue(a.GetValueAsFloat() - b.GetValueAsFloat());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator *(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return new DreamValue(a._floatValue * b._floatValue);
            return new DreamValue(a.GetValueAsFloat() * b.GetValueAsFloat());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator /(DreamValue a, DreamValue b)
        {
            float floatB = b.Type == DreamValueType.Float ? b._floatValue : b.GetValueAsFloat();
            if (floatB == 0)
                return new DreamValue(0);

            float floatA = a.Type == DreamValueType.Float ? a._floatValue : a.GetValueAsFloat();
            return new DreamValue(floatA / floatB);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator %(DreamValue a, DreamValue b)
        {
            float floatB = b.Type == DreamValueType.Float ? b._floatValue : b.GetValueAsFloat();
            if (floatB == 0)
                return new DreamValue(0);

            float floatA = a.Type == DreamValueType.Float ? a._floatValue : a.GetValueAsFloat();
            return new DreamValue(floatA % floatB);
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
                return _floatValue == other._floatValue || MathF.Abs(_floatValue - other._floatValue) < 0.00001f;

            if (Type == DreamValueType.Null) return true;

            return ReferenceEquals(_objectValue, other._objectValue) || (_objectValue?.Equals(other._objectValue) ?? false);
        }

        public override int GetHashCode()
        {
            if (Type == DreamValueType.Float) return _floatValue.GetHashCode();
            if (Type == DreamValueType.Null) return 0;

            // Manual hash combination for speed
            int hash = (int)Type;
            if (_objectValue != null) hash = (hash * 397) ^ _objectValue.GetHashCode();
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(DreamValue a, DreamValue b)
        {
            if (a.Type == b.Type)
            {
                if (a.Type == DreamValueType.Float)
                    return a._floatValue == b._floatValue || MathF.Abs(a._floatValue - b._floatValue) < 0.00001f;

                if (a.Type == DreamValueType.Null) return true;

                return ReferenceEquals(a._objectValue, b._objectValue) || (a._objectValue?.Equals(b._objectValue) ?? false);
            }

            // DM Parity: null == 0
            if (a.Type == DreamValueType.Null)
                return b.Type == DreamValueType.Float && b._floatValue == 0;
            if (b.Type == DreamValueType.Null)
                return a.Type == DreamValueType.Float && a._floatValue == 0;

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
            return a.GetValueAsFloat() > b.GetValueAsFloat();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return a._floatValue < b._floatValue;
            if (a.Type == DreamValueType.String && b.Type == DreamValueType.String)
                return string.CompareOrdinal((string)a._objectValue!, (string)b._objectValue!) < 0;
            return a.GetValueAsFloat() < b.GetValueAsFloat();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return a._floatValue >= b._floatValue;
            if (a.Type == DreamValueType.String && b.Type == DreamValueType.String)
                return string.CompareOrdinal((string)a._objectValue!, (string)b._objectValue!) >= 0;
            return a.GetValueAsFloat() >= b.GetValueAsFloat();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return a._floatValue <= b._floatValue;
            if (a.Type == DreamValueType.String && b.Type == DreamValueType.String)
                return string.CompareOrdinal((string)a._objectValue!, (string)b._objectValue!) <= 0;
            return a.GetValueAsFloat() <= b.GetValueAsFloat();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator -(DreamValue a)
        {
            return new DreamValue(-a.GetValueAsFloat());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator !(DreamValue a)
        {
            return new DreamValue(a.IsFalse() ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator &(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return new DreamValue(((int)a._floatValue & (int)b._floatValue) & 0x00FFFFFF);
            return new DreamValue(((int)a.GetValueAsFloat() & (int)b.GetValueAsFloat()) & 0x00FFFFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator |(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return new DreamValue(((int)a._floatValue | (int)b._floatValue) & 0x00FFFFFF);
            return new DreamValue(((int)a.GetValueAsFloat() | (int)b.GetValueAsFloat()) & 0x00FFFFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator ^(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                return new DreamValue(((int)a._floatValue ^ (int)b._floatValue) & 0x00FFFFFF);
            return new DreamValue(((int)a.GetValueAsFloat() ^ (int)b.GetValueAsFloat()) & 0x00FFFFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator ~(DreamValue a)
        {
            if (a.Type == DreamValueType.Float)
                return new DreamValue((~(int)a._floatValue) & 0x00FFFFFF);
            return new DreamValue((~(int)a.GetValueAsFloat()) & 0x00FFFFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator <<(DreamValue a, DreamValue b)
        {
            int valA = (a.Type == DreamValueType.Float) ? (int)a._floatValue : (int)a.GetValueAsFloat();
            int valB = (b.Type == DreamValueType.Float) ? (int)b._floatValue : (int)b.GetValueAsFloat();
            return new DreamValue(SharedOperations.BitShiftLeft(valA, valB));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DreamValue operator >>(DreamValue a, DreamValue b)
        {
            int valA = (a.Type == DreamValueType.Float) ? (int)a._floatValue : (int)a.GetValueAsFloat();
            int valB = (b.Type == DreamValueType.Float) ? (int)b._floatValue : (int)b.GetValueAsFloat();
            return new DreamValue(SharedOperations.BitShiftRight(valA, valB));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFalse()
        {
            return Type switch
            {
                DreamValueType.Null => true,
                DreamValueType.Float => _floatValue == 0,
                DreamValueType.String => ((string)_objectValue!).Length == 0,
                _ => false
            };
        }

        public void WriteTo(Utf8JsonWriter writer, JsonSerializerOptions options)
        {
            switch (Type)
            {
                case DreamValueType.Float:
                    writer.WriteNumberValue(_floatValue);
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

        public void WriteTo(System.IO.BinaryWriter writer)
        {
            writer.Write((byte)Type);
            switch (Type)
            {
                case DreamValueType.Float:
                    writer.Write(_floatValue);
                    break;
                case DreamValueType.String:
                    writer.Write((string)_objectValue!);
                    break;
                case DreamValueType.Null:
                    break;
                case DreamValueType.DreamObject:
                    if (_objectValue is GameObject g)
                    {
                        writer.Write(true);
                        writer.Write(g.Id);
                    }
                    else
                    {
                        writer.Write(false);
                        writer.Write(ToString());
                    }
                    break;
                default:
                    writer.Write(ToString());
                    break;
            }
        }

        public static DreamValue ReadFrom(System.IO.BinaryReader reader)
        {
            var type = (DreamValueType)reader.ReadByte();
            switch (type)
            {
                case DreamValueType.Float:
                    return new DreamValue(reader.ReadSingle());
                case DreamValueType.String:
                    return new DreamValue(reader.ReadString());
                case DreamValueType.Null:
                    return Null;
                case DreamValueType.DreamObject:
                    if (reader.ReadBoolean())
                    {
                        // We can't resolve GameObject here easily without a context,
                        // so we might just store the ID for now or return a placeholder.
                        return new DreamValue((float)reader.ReadInt32()); // Placeholder for ID
                    }
                    return new DreamValue(reader.ReadString());
                default:
                    return new DreamValue(reader.ReadString());
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
                    return new DreamValue(reader.GetSingle());
                case JsonTokenType.String:
                    return new DreamValue(reader.GetString()!);
                case JsonTokenType.True:
                    return new DreamValue(1.0f);
                case JsonTokenType.False:
                    return new DreamValue(0.0f);
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
}
