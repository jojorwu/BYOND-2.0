using Shared;

namespace DMCompiler.Bytecode;

// ReSharper disable MissingBlankLines
public enum DreamProcOpcode : byte {
    [OpcodeMetadata(-1)]
    BitShiftLeft = Opcode.BitShiftLeft,
    [OpcodeMetadata(1, OpcodeArgType.TypeId)]
    PushType = Opcode.PushType,
    [OpcodeMetadata(1, OpcodeArgType.String)]
    PushString = Opcode.PushString,
    [OpcodeMetadata(0, OpcodeArgType.String, OpcodeArgType.FormatCount)]
    FormatString = Opcode.FormatString,
    [OpcodeMetadata(-2, OpcodeArgType.Label)]
    SwitchCaseRange = Opcode.SwitchCaseRange, //This could either shrink the stack by 2 or 3. Assume 2.
    [OpcodeMetadata(1, OpcodeArgType.Reference)]
    PushReferenceValue = Opcode.PushReferenceValue, // TODO: Local refs should be pure, and other refs that aren't modified
    [OpcodeMetadata(0, OpcodeArgType.ArgType, OpcodeArgType.StackDelta)]
    Rgb = Opcode.Rgb,
    [OpcodeMetadata(-1)]
    Add = Opcode.Add,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    Assign = Opcode.Assign,
    [OpcodeMetadata(0, OpcodeArgType.Reference, OpcodeArgType.ArgType, OpcodeArgType.StackDelta, OpcodeArgType.StackDelta)]
    Call = Opcode.Call,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    MultiplyReference = Opcode.MultiplyReference,
    [OpcodeMetadata(-1, OpcodeArgType.Label)]
    JumpIfFalse = Opcode.JumpIfFalse,
    [OpcodeMetadata(0, OpcodeArgType.ListSize)]
    CreateStrictAssociativeList = Opcode.CreateStrictAssociativeList,
    [OpcodeMetadata(0, OpcodeArgType.Label)]
    Jump = Opcode.Jump,
    [OpcodeMetadata(-1)]
    CompareEquals = Opcode.CompareEquals,
    [OpcodeMetadata(-1)]
    Return = Opcode.Return,
    [OpcodeMetadata(1)]
    PushNull = Opcode.PushNull,
    [OpcodeMetadata(-1)]
    Subtract = Opcode.Subtract,
    [OpcodeMetadata(-1)]
    CompareLessThan = Opcode.CompareLessThan,
    [OpcodeMetadata(-1)]
    CompareGreaterThan = Opcode.CompareGreaterThan,
    [OpcodeMetadata(-1, OpcodeArgType.Label)]
    BooleanAnd = Opcode.BooleanAnd, //Either shrinks the stack 1 or 0. Assume 1.
    [OpcodeMetadata]
    BooleanNot = Opcode.BooleanNot,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    DivideReference = Opcode.DivideReference,
    [OpcodeMetadata]
    Negate = Opcode.Negate,
    [OpcodeMetadata(-1)]
    Modulus = Opcode.Modulus,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    Append = Opcode.Append,
    [OpcodeMetadata(-3, OpcodeArgType.EnumeratorId)]
    CreateRangeEnumerator = Opcode.CreateRangeEnumerator,
    [OpcodeMetadata(0, OpcodeArgType.Reference, OpcodeArgType.Reference)]
    Input = Opcode.Input,
    [OpcodeMetadata(-1)]
    CompareLessThanOrEqual = Opcode.CompareLessThanOrEqual,
    [OpcodeMetadata(0, OpcodeArgType.ListSize)]
    CreateAssociativeList = Opcode.CreateAssociativeList,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    Remove = Opcode.Remove,
    [OpcodeMetadata(-1)]
    DeleteObject = Opcode.DeleteObject,
    [OpcodeMetadata(1, OpcodeArgType.Resource)]
    PushResource = Opcode.PushResource,
    [OpcodeMetadata(0, OpcodeArgType.ListSize)]
    CreateList = Opcode.CreateList,
    [OpcodeMetadata(0, OpcodeArgType.ArgType, OpcodeArgType.StackDelta)]
    CallStatement = Opcode.CallStatement,
    [OpcodeMetadata(-1)]
    BitAnd = Opcode.BitAnd,
    [OpcodeMetadata(-1)]
    CompareNotEquals = Opcode.CompareNotEquals,
    [OpcodeMetadata(1, OpcodeArgType.ProcId)]
    PushProc = Opcode.PushProc,
    [OpcodeMetadata(-1)]
    Divide = Opcode.Divide,
    [OpcodeMetadata(-1)]
    Multiply = Opcode.Multiply,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    BitXorReference = Opcode.BitXorReference,
    [OpcodeMetadata(-1)]
    BitXor = Opcode.BitXor,
    [OpcodeMetadata(-1)]
    BitOr = Opcode.BitOr,
    [OpcodeMetadata]
    BitNot = Opcode.BitNot,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    Combine = Opcode.Combine,
    [OpcodeMetadata(0, OpcodeArgType.ArgType, OpcodeArgType.StackDelta)]
    CreateObject = Opcode.CreateObject,
    [OpcodeMetadata(-1, OpcodeArgType.Label)]
    BooleanOr = Opcode.BooleanOr, // Shrinks the stack by 1 or 0. Assume 1.
    [OpcodeMetadata(0, OpcodeArgType.ListSize)]
    CreateMultidimensionalList = Opcode.CreateMultidimensionalList,
    [OpcodeMetadata(-1)]
    CompareGreaterThanOrEqual = Opcode.CompareGreaterThanOrEqual,
    [OpcodeMetadata(-1, OpcodeArgType.Label)]
    SwitchCase = Opcode.SwitchCase, //This could either shrink the stack by 1 or 2. Assume 1.
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    Mask = Opcode.Mask,
    [OpcodeMetadata]
    Error = Opcode.Error,
    [OpcodeMetadata(-1)]
    IsInList = Opcode.IsInList,
    [OpcodeMetadata(1, OpcodeArgType.Float)]
    PushFloat = Opcode.PushFloat,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    ModulusReference = Opcode.ModulusReference,
    [OpcodeMetadata(-1, OpcodeArgType.EnumeratorId)]
    CreateListEnumerator = Opcode.CreateListEnumerator,
    [OpcodeMetadata(0, OpcodeArgType.EnumeratorId, OpcodeArgType.Reference, OpcodeArgType.Label)]
    Enumerate = Opcode.Enumerate,
    [OpcodeMetadata(0, OpcodeArgType.EnumeratorId)]
    DestroyEnumerator = Opcode.DestroyEnumerator,
    [OpcodeMetadata(-3)]
    Browse = Opcode.Browse,
    [OpcodeMetadata(-3)]
    BrowseResource = Opcode.BrowseResource,
    [OpcodeMetadata(-3)]
    OutputControl = Opcode.OutputControl,
    [OpcodeMetadata(-1)]
    BitShiftRight = Opcode.BitShiftRight,
    [OpcodeMetadata(-1, OpcodeArgType.EnumeratorId, OpcodeArgType.FilterId)]
    CreateFilteredListEnumerator = Opcode.CreateFilteredListEnumerator,
    [OpcodeMetadata(-1)]
    Power = Opcode.Power,
    [OpcodeMetadata(0, OpcodeArgType.EnumeratorId, OpcodeArgType.Reference, OpcodeArgType.Reference, OpcodeArgType.Label)]
    EnumerateAssoc = Opcode.EnumerateAssoc,
    [OpcodeMetadata(-2)]
    Link = Opcode.Link,
    [OpcodeMetadata(-3, OpcodeArgType.TypeId)]
    Prompt = Opcode.Prompt,
    [OpcodeMetadata(-3)]
    Ftp = Opcode.Ftp,
    [OpcodeMetadata(-1)]
    Initial = Opcode.Initial,
    [OpcodeMetadata(-1)]
    AsType = Opcode.AsType,
    [OpcodeMetadata(-1)]
    IsType = Opcode.IsType,
    [OpcodeMetadata(-2)]
    LocateCoord = Opcode.LocateCoord,
    [OpcodeMetadata(-1)]
    Locate = Opcode.Locate,
    [OpcodeMetadata]
    IsNull = Opcode.IsNull,
    [OpcodeMetadata(-1, OpcodeArgType.Label)]
    Spawn = Opcode.Spawn,
    [OpcodeMetadata(-1, OpcodeArgType.Reference)]
    OutputReference = Opcode.OutputReference,
    [OpcodeMetadata(-2)]
    Output = Opcode.Output,
    [OpcodeMetadata(-1)]
    Pop = Opcode.Pop,
    [OpcodeMetadata]
    Prob = Opcode.Prob,
    [OpcodeMetadata(-1)]
    IsSaved = Opcode.IsSaved,
    [OpcodeMetadata(0, OpcodeArgType.PickCount)]
    PickUnweighted = Opcode.PickUnweighted,
    [OpcodeMetadata(0, OpcodeArgType.PickCount)]
    PickWeighted = Opcode.PickWeighted,
    [OpcodeMetadata(1, OpcodeArgType.Reference)]
    Increment = Opcode.Increment,
    [OpcodeMetadata(1, OpcodeArgType.Reference)]
    Decrement = Opcode.Decrement,
    [OpcodeMetadata(-1)]
    CompareEquivalent = Opcode.CompareEquivalent,
    [OpcodeMetadata(-1)]
    CompareNotEquivalent = Opcode.CompareNotEquivalent,
    [OpcodeMetadata]
    Throw = Opcode.Throw,
    [OpcodeMetadata(-2)]
    IsInRange = Opcode.IsInRange,
    [OpcodeMetadata(0, OpcodeArgType.ConcatCount)]
    MassConcatenation = Opcode.MassConcatenation,
    [OpcodeMetadata(-1, OpcodeArgType.EnumeratorId)]
    CreateTypeEnumerator = Opcode.CreateTypeEnumerator,
    [OpcodeMetadata(1)]
    PushGlobalVars = Opcode.PushGlobalVars,
    [OpcodeMetadata(-1)]
    ModulusModulus = Opcode.ModulusModulus,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    ModulusModulusReference = Opcode.ModulusModulusReference,
    [OpcodeMetadata(0, OpcodeArgType.Label)]
    JumpIfNull = Opcode.JumpIfNull,
    [OpcodeMetadata(0, OpcodeArgType.Label)]
    JumpIfNullNoPop = Opcode.JumpIfNullNoPop,
    [OpcodeMetadata(0, OpcodeArgType.Reference, OpcodeArgType.Label)]
    JumpIfTrueReference = Opcode.JumpIfTrueReference,
    [OpcodeMetadata(0, OpcodeArgType.Reference, OpcodeArgType.Label)]
    JumpIfFalseReference = Opcode.JumpIfFalseReference,
    [OpcodeMetadata(0, OpcodeArgType.String)]
    DereferenceField = Opcode.DereferenceField,
    [OpcodeMetadata(-1)]
    DereferenceIndex = Opcode.DereferenceIndex,
    [OpcodeMetadata(0, OpcodeArgType.String, OpcodeArgType.ArgType, OpcodeArgType.StackDelta)]
    DereferenceCall = Opcode.DereferenceCall,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    PopReference = Opcode.PopReference,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    BitShiftLeftReference = Opcode.BitShiftLeftReference,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    BitShiftRightReference = Opcode.BitShiftRightReference,
    [OpcodeMetadata(0, OpcodeArgType.Label, OpcodeArgType.Reference)]
    Try = Opcode.Try,
    [OpcodeMetadata(0, OpcodeArgType.Label)]
    TryNoValue = Opcode.TryNoValue,
    [OpcodeMetadata]
    EndTry = Opcode.EndTry,
    [OpcodeMetadata(0, OpcodeArgType.EnumeratorId, OpcodeArgType.Label)]
    EnumerateNoAssign = Opcode.EnumerateNoAssign,
    [OpcodeMetadata(0, OpcodeArgType.ArgType, OpcodeArgType.StackDelta)]
    Gradient = Opcode.Gradient,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    AssignInto = Opcode.AssignInto,
    [OpcodeMetadata(-1)]
    GetStep = Opcode.GetStep,
    [OpcodeMetadata]
    Length = Opcode.Length,
    [OpcodeMetadata(-1)]
    GetDir = Opcode.GetDir,
    [OpcodeMetadata]
    DebuggerBreakpoint = Opcode.DebuggerBreakpoint,
    [OpcodeMetadata]
    Sin = Opcode.Sin,
    [OpcodeMetadata]
    Cos = Opcode.Cos,
    [OpcodeMetadata]
    Tan = Opcode.Tan,
    [OpcodeMetadata]
    ArcSin = Opcode.ArcSin,
    [OpcodeMetadata]
    ArcCos = Opcode.ArcCos,
    [OpcodeMetadata]
    ArcTan = Opcode.ArcTan,
    [OpcodeMetadata(-1)]
    ArcTan2 = Opcode.ArcTan2,
    [OpcodeMetadata]
    Sqrt = Opcode.Sqrt,
    [OpcodeMetadata(-1)]
    Log = Opcode.Log,
    [OpcodeMetadata]
    LogE = Opcode.LogE,
    [OpcodeMetadata]
    Abs = Opcode.Abs,
    [OpcodeMetadata(-1, OpcodeArgType.Reference)]
    AppendNoPush = Opcode.AppendNoPush,
    [OpcodeMetadata(-1, OpcodeArgType.Reference)]
    AssignNoPush = Opcode.AssignNoPush,
    [OpcodeMetadata(1, OpcodeArgType.Reference, OpcodeArgType.String)]
    PushRefAndDereferenceField = Opcode.PushRefAndDereferenceField,
    [OpcodeMetadata(true, 0, OpcodeArgType.Int)]
    PushNRefs = Opcode.PushNRefs,
    [OpcodeMetadata(true, 0, OpcodeArgType.Int)]
    PushNFloats = Opcode.PushNFloats,
    [OpcodeMetadata(true, 0, OpcodeArgType.Int)]
    PushNResources = Opcode.PushNResources,
    [OpcodeMetadata(2, OpcodeArgType.String, OpcodeArgType.Float)]
    PushStringFloat = Opcode.PushStringFloat,
    [OpcodeMetadata(0, OpcodeArgType.Reference, OpcodeArgType.Label)]
    JumpIfReferenceFalse = Opcode.JumpIfReferenceFalse,
    [OpcodeMetadata(true, 0, OpcodeArgType.Int)]
    PushNStrings = Opcode.PushNStrings,
    [OpcodeMetadata(0, OpcodeArgType.Float, OpcodeArgType.Label)]
    SwitchOnFloat = Opcode.SwitchOnFloat,
    [OpcodeMetadata(true, 0, OpcodeArgType.Int)]
    PushNOfStringFloats = Opcode.PushNOfStringFloats,
    [OpcodeMetadata(true, 1, OpcodeArgType.Int)]
    CreateListNFloats = Opcode.CreateListNFloats,
    [OpcodeMetadata(true, 1, OpcodeArgType.Int)]
    CreateListNStrings = Opcode.CreateListNStrings,
    [OpcodeMetadata(true, 1, OpcodeArgType.Int)]
    CreateListNRefs = Opcode.CreateListNRefs,
    [OpcodeMetadata(true, 1, OpcodeArgType.Int)]
    CreateListNResources = Opcode.CreateListNResources,
    [OpcodeMetadata(0, OpcodeArgType.String, OpcodeArgType.Label)]
    SwitchOnString = Opcode.SwitchOnString,
    [OpcodeMetadata(0, OpcodeArgType.TypeId)]
    IsTypeDirect = Opcode.IsTypeDirect,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    NullRef = Opcode.NullRef,
    [OpcodeMetadata(0, OpcodeArgType.Reference)]
    ReturnReferenceValue = Opcode.ReturnReferenceValue,
    [OpcodeMetadata(0, OpcodeArgType.Float)]
    ReturnFloat = Opcode.ReturnFloat,
    [OpcodeMetadata(1, OpcodeArgType.Reference, OpcodeArgType.String)]
    IndexRefWithString = Opcode.IndexRefWithString,
    [OpcodeMetadata(2, OpcodeArgType.Float, OpcodeArgType.Reference)]
    PushFloatAssign = Opcode.PushFloatAssign,
    [OpcodeMetadata(true, 0, OpcodeArgType.Int)]
    NPushFloatAssign = Opcode.NPushFloatAssign
}
// ReSharper restore MissingBlankLines

/// <summary>
/// An operand given to opcodes that call a proc with arguments.
/// Determines where the arguments come from.
/// </summary>
public enum DMCallArgumentsType {
    // There are no arguments
    None,

    // The arguments are stored on the stack
    FromStack,

    // Also stored on the stack, but every arg has a key associated with it (named arguments)
    FromStackKeyed,

    // Arguments are provided from a list on the top of the stack ( arglist() )
    FromArgumentList,

    // Same arguments as the ones given to the proc doing the calling (implicit ..() arguments)
    FromProcArguments
}
