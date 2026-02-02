using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System;

namespace Core.Dmi;

[Flags]
public enum AtomDirection : byte {
    None = 0,

    North = 1,
    South = 2,
    East = 4,
    West = 8,

    Up = 16,
    Down = 32,

    Northeast = North | East,
    Southeast = South | East,
    Southwest = South | West,
    Northwest = North | West
}
