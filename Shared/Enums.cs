using System;

namespace Shared
{
    [Flags]
    public enum ProcAttributes
    {
        None = 0,
        IsAggregated = 1,
        Unimplemented = 2,
        Stub = 4
    }

    public enum DMValueType
    {
        Anything = 0x0,
        Null = 0x1,
        Turf = 0x2,
        Obj = 0x3,
        Mob = 0x4,
        Area = 0x5,
        Client = 0x6,
        String = 0x7,
        Num = 0x8,
        Icon = 0x9,
        Image = 0xA,
        MutableAppearance = 0xB,
        Sound = 0xC,
        File = 0xD,
        Path = 0xE,
        List = 0xF,
        Appearance = 0x10,
        Datum = 0x11,
        Savefile = 0x12,
        Filter = 0x13,
        Input = 0x14
    }
}
