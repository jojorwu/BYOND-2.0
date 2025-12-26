using System;
using System.Globalization;

namespace Core.VM.Types
{
    public readonly struct DreamValue
    {
        public readonly DreamValueType Type;
        private readonly float _floatValue;
        private readonly object? _objectValue;

        public static readonly DreamValue Null = new DreamValue();

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

        public bool TryGetValue(out string? value)
        {
            if (Type == DreamValueType.String)
            {
                value = (string?)_objectValue;
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetValue(out DreamObject? value)
        {
            if (Type == DreamValueType.DreamObject)
            {
                value = (DreamObject?)_objectValue;
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

        private float AsFloat()
        {
            if (Type != DreamValueType.Float)
                throw new InvalidOperationException($"Cannot read DreamValue of type {Type} as a float");
            return _floatValue;
        }

        public static implicit operator DreamValue(float value) => new DreamValue(value);
        public static implicit operator DreamValue(int value) => new DreamValue((float)value);
        public static implicit operator DreamValue(string value) => new DreamValue(value);
        public static implicit operator DreamValue(DreamObject value) => new DreamValue(value);

        public static DreamValue FromObject(object? value)
        {
            return value switch
            {
                null => Null,
                string s => new DreamValue(s),
                int i => new DreamValue(i),
                float f => new DreamValue(f),
                DreamObject o => new DreamValue(o),
                _ => throw new ArgumentException($"Unsupported type for DreamValue: {value.GetType()}")
            };
        }

        public override string ToString()
        {
            if (TryGetValue(out float floatValue))
            {
                return floatValue.ToString(CultureInfo.InvariantCulture);
            }
            if (TryGetValue(out string? stringValue))
            {
                return stringValue ?? string.Empty;
            }
            if (TryGetValue(out DreamObject? dreamObjectValue))
            {
                return dreamObjectValue?.ToString() ?? "null";
            }
            if (Type == DreamValueType.Null)
            {
                return "null";
            }
            throw new InvalidOperationException("Invalid DreamValue type");
        }

        public static DreamValue operator +(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.String || b.Type == DreamValueType.String)
            {
                var sA = a.Type == DreamValueType.String ? (string)a._objectValue! : a.ToString();
                var sB = b.Type == DreamValueType.String ? (string)b._objectValue! : b.ToString();
                return new DreamValue(sA + sB);
            }

            return new DreamValue(a.AsFloat() + b.AsFloat());
        }

        public static DreamValue operator -(DreamValue a, DreamValue b)
        {
            return new DreamValue(a.AsFloat() - b.AsFloat());
        }

        public static DreamValue operator *(DreamValue a, DreamValue b)
        {
            return new DreamValue(a.AsFloat() * b.AsFloat());
        }

        public static DreamValue operator /(DreamValue a, DreamValue b)
        {
            var floatB = b.AsFloat();
            if (floatB == 0)
                return new DreamValue(0); // Avoid division by zero

            return new DreamValue(a.AsFloat() / floatB);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not DreamValue other)
                return false;

            if (Type != other.Type)
                return false;

            switch (Type)
            {
                case DreamValueType.Null:
                    return true;
                case DreamValueType.Float:
                    return Math.Abs(_floatValue - other._floatValue) < 0.00001f;
                case DreamValueType.String:
                case DreamValueType.DreamObject:
                    return _objectValue?.Equals(other._objectValue) ?? other._objectValue == null;
                default:
                    throw new InvalidOperationException("Invalid DreamValue type");
            }
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
            return a.AsFloat() > b.AsFloat();
        }

        public static bool operator <(DreamValue a, DreamValue b)
        {
            return a.AsFloat() < b.AsFloat();
        }

        public static bool operator >=(DreamValue a, DreamValue b)
        {
            return a.AsFloat() >= b.AsFloat();
        }

        public static bool operator <=(DreamValue a, DreamValue b)
        {
            return a.AsFloat() <= b.AsFloat();
        }

        public static DreamValue operator -(DreamValue a)
        {
            return new DreamValue(-a.AsFloat());
        }

        public static DreamValue operator !(DreamValue a)
        {
            return new DreamValue(a.IsFalse() ? 1 : 0);
        }

        public static DreamValue operator &(DreamValue a, DreamValue b)
        {
            return new DreamValue((int)a.AsFloat() & (int)b.AsFloat());
        }

        public static DreamValue operator |(DreamValue a, DreamValue b)
        {
            return new DreamValue((int)a.AsFloat() | (int)b.AsFloat());
        }

        public static DreamValue operator ^(DreamValue a, DreamValue b)
        {
            return new DreamValue((int)a.AsFloat() ^ (int)b.AsFloat());
        }

        public static DreamValue operator ~(DreamValue a)
        {
            return new DreamValue(~(int)a.AsFloat());
        }

        public static DreamValue operator <<(DreamValue a, DreamValue b)
        {
            return new DreamValue((int)a.AsFloat() << (int)b.AsFloat());
        }

        public static DreamValue operator >>(DreamValue a, DreamValue b)
        {
            return new DreamValue((int)a.AsFloat() >> (int)b.AsFloat());
        }

        public bool IsFalse()
        {
            if (Type == DreamValueType.Null)
                return true;

            if (Type == DreamValueType.Float && _floatValue == 0)
                return true;

            if (Type == DreamValueType.String && (string)_objectValue! == "")
                return true;

            return false;
        }
    }
}
