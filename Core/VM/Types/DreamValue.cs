using System;
using System.Globalization;

namespace Core.VM.Types
{
    public readonly struct DreamValue
    {
        public readonly DreamValueType Type;
        private readonly object _value;

        public static readonly DreamValue Null = new DreamValue();

        public DreamValue()
        {
            Type = DreamValueType.Null;
            _value = null;
        }

        public DreamValue(float value)
        {
            Type = DreamValueType.Float;
            _value = value;
        }

        public DreamValue(string value)
        {
            Type = DreamValueType.String;
            _value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public DreamValue(DreamObject value)
        {
            Type = DreamValueType.DreamObject;
            _value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool TryGetValue(out float value)
        {
            if (Type == DreamValueType.Float)
            {
                value = (float)_value;
                return true;
            }

            value = 0;
            return false;
        }

        public bool TryGetValue(out string value)
        {
            if (Type == DreamValueType.String)
            {
                value = (string)_value;
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetValue(out DreamObject value)
        {
            if (Type == DreamValueType.DreamObject)
            {
                value = (DreamObject)_value;
                return true;
            }

            value = null;
            return false;
        }


        public static implicit operator DreamValue(float value) => new DreamValue(value);
        public static implicit operator DreamValue(int value) => new DreamValue((float)value);
        public static implicit operator DreamValue(string value) => new DreamValue(value);
        public static implicit operator DreamValue(DreamObject value) => new DreamValue(value);

        public override string ToString()
        {
            switch (Type)
            {
                case DreamValueType.Null:
                    return "null";
                case DreamValueType.Float:
                    return ((float)_value).ToString(CultureInfo.InvariantCulture);
                case DreamValueType.String:
                    return (string)_value;
                case DreamValueType.DreamObject:
                    return _value.ToString();
                default:
                    throw new InvalidOperationException("Invalid DreamValue type");
            }
        }

        public static DreamValue operator +(DreamValue a, DreamValue b)
        {
            if (a.Type == DreamValueType.String || b.Type == DreamValueType.String)
            {
                return new DreamValue(a.ToString() + b.ToString());
            }

            float floatA = 0;
            if (a.Type == DreamValueType.Float)
                floatA = (float)a._value;

            float floatB = 0;
            if (b.Type == DreamValueType.Float)
                floatB = (float)b._value;

            return new DreamValue(floatA + floatB);
        }
    }
}
