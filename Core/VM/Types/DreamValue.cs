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
                    return _floatValue.ToString(CultureInfo.InvariantCulture);
                case DreamValueType.String:
                    return (string?)_objectValue ?? string.Empty;
                case DreamValueType.DreamObject:
                    return _objectValue?.ToString() ?? "null";
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
                floatA = a._floatValue;

            float floatB = 0;
            if (b.Type == DreamValueType.Float)
                floatB = b._floatValue;

            return new DreamValue(floatA + floatB);
        }
    }
}
