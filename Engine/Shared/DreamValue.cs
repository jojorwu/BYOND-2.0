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

        public bool IsNull => Type == DreamValueType.Null;

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

        public DreamObject? GetValueAsDreamObject()
        {
            if (Type == DreamValueType.DreamObject)
            {
                return (DreamObject?)_objectValue;
            }

            return null;
        }

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

        public float RawFloat => _floatValue;

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

        public override string ToString()
        {
            return Type switch
            {
                DreamValueType.Float => _floatValue.ToString(CultureInfo.InvariantCulture),
                DreamValueType.String => (string)_objectValue!,
                DreamValueType.DreamObject => _objectValue?.ToString() ?? "null",
                DreamValueType.Null => "null",
                _ => _objectValue?.ToString() ?? Type.ToString()
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetValueAsFloat()
        {
            return Type switch
            {
                DreamValueType.Float => _floatValue,
                DreamValueType.Null => 0,
                _ => _floatValue
            };
        }

        public static DreamValue operator +(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.String || b.Type == DreamValueType.String)
            {
                var sA = a.ToString();
                var sB = b.ToString();
                return new DreamValue(sA + sB);
            }

            return new DreamValue(a.GetValueAsFloat() + b.GetValueAsFloat());
        }

        public static DreamValue operator -(DreamValue a, DreamValue b)
        {
            return new DreamValue(a.GetValueAsFloat() - b.GetValueAsFloat());
        }

        public static DreamValue operator *(DreamValue a, DreamValue b)
        {
            return new DreamValue(a.GetValueAsFloat() * b.GetValueAsFloat());
        }

        public static DreamValue operator /(DreamValue a, DreamValue b)
        {
            var floatB = b.GetValueAsFloat();
            if (floatB == 0)
                return new DreamValue(0); // Avoid division by zero

            return new DreamValue(a.GetValueAsFloat() / floatB);
        }

        public override bool Equals(object? obj) => obj is DreamValue other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DreamValue other)
        {
            if (Type != other.Type)
                return false;

            return Type switch
            {
                DreamValueType.Null => true,
                DreamValueType.Float => Math.Abs(_floatValue - other._floatValue) < 0.00001f,
                DreamValueType.String or DreamValueType.DreamObject or DreamValueType.DreamType or DreamValueType.DreamResource or DreamValueType.DreamProc =>
                    ReferenceEquals(_objectValue, other._objectValue) || (_objectValue?.Equals(other._objectValue) ?? false),
                _ => false
            };
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, _floatValue, _objectValue);
        }

        public static bool operator ==(DreamValue a, DreamValue b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(DreamValue a, DreamValue b)
        {
            return !a.Equals(b);
        }

        public static bool operator >(DreamValue a, DreamValue b)
        {
            return a.GetValueAsFloat() > b.GetValueAsFloat();
        }

        public static bool operator <(DreamValue a, DreamValue b)
        {
            return a.GetValueAsFloat() < b.GetValueAsFloat();
        }

        public static bool operator >=(DreamValue a, DreamValue b)
        {
            return a.GetValueAsFloat() >= b.GetValueAsFloat();
        }

        public static bool operator <=(DreamValue a, DreamValue b)
        {
            return a.GetValueAsFloat() <= b.GetValueAsFloat();
        }

        public static DreamValue operator -(DreamValue a)
        {
            return new DreamValue(-a.GetValueAsFloat());
        }

        public static DreamValue operator !(DreamValue a)
        {
            return new DreamValue(a.IsFalse() ? 1 : 0);
        }

        public static DreamValue operator &(DreamValue a, DreamValue b)
        {
            return new DreamValue((int)a.GetValueAsFloat() & (int)b.GetValueAsFloat());
        }

        public static DreamValue operator |(DreamValue a, DreamValue b)
        {
            return new DreamValue((int)a.GetValueAsFloat() | (int)b.GetValueAsFloat());
        }

        public static DreamValue operator ^(DreamValue a, DreamValue b)
        {
            return new DreamValue((int)a.GetValueAsFloat() ^ (int)b.GetValueAsFloat());
        }

        public static DreamValue operator ~(DreamValue a)
        {
            return new DreamValue(~(int)a.GetValueAsFloat());
        }

        public static DreamValue operator <<(DreamValue a, DreamValue b)
        {
            return new DreamValue(SharedOperations.BitShiftLeft((int)a.GetValueAsFloat(), (int)b.GetValueAsFloat()));
        }

        public static DreamValue operator >>(DreamValue a, DreamValue b)
        {
            return new DreamValue(SharedOperations.BitShiftRight((int)a.GetValueAsFloat(), (int)b.GetValueAsFloat()));
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
            switch (value.Type)
            {
                case DreamValueType.Float:
                    writer.WriteNumberValue(value.AsFloat());
                    break;
                case DreamValueType.String:
                    value.TryGetValue(out string? s);
                    writer.WriteStringValue(s);
                    break;
                case DreamValueType.Null:
                    writer.WriteNullValue();
                    break;
                default:
                    writer.WriteStringValue(value.ToString());
                    break;
            }
        }
    }
}
