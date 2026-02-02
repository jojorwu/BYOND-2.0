using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
﻿namespace DMCompiler.DM;

/// <summary>
/// The value of "set src = ..." in a verb
/// </summary>
public enum VerbSrc {
    View,
    InView,
    OView,
    InOView,
    Range,
    InRange,
    ORange,
    InORange,
    World,
    InWorld,
    Usr,
    InUsr,
    UsrLoc,
    UsrGroup
}
