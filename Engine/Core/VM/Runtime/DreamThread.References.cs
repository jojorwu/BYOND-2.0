using Shared.Enums;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public partial class DreamThread
{
    /// <summary>
    /// Reads a reference from the bytecode stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DMReference ReadReference(ReadOnlySpan<byte> bytecode, ref int pc)
    {
        var refType = (DMReference.Type)bytecode[pc++];
        if (refType == DMReference.Type.Local || refType == DMReference.Type.Argument)
        {
            var idx = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
            pc += 4;
            return new DMReference { RefType = refType, Index = idx };
        }

        if (refType >= DMReference.Type.Global && refType <= DMReference.Type.GlobalProc)
        {
            var globalIdx = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
            pc += 4;
            return new DMReference { RefType = refType, Index = globalIdx };
        }

        if (refType >= DMReference.Type.Field && refType <= DMReference.Type.SrcField)
        {
            var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
            pc += 4;
            if (nameId < 0 || nameId >= Context.Strings.Count)
                throw new ScriptRuntimeException($"String index out of bounds: {nameId}", null, pc, this);

            var name = Context.Strings[nameId];
            if (name == null)
                throw new ScriptRuntimeException($"Null string at index: {nameId}", null, pc, this);

            return new DMReference { RefType = refType, Name = name };
        }

        return new DMReference { RefType = refType };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetReferenceStackSize(DMReference reference)
    {
        return reference.RefType switch
        {
            DMReference.Type.Field => 1,
            DMReference.Type.ListIndex => 2,
            _ => 0
        };
    }

    /// <summary>
    /// Resolves a reference to its current value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamValue GetReferenceValue(DMReference reference, ref CallFrame frame, int stackOffset = 0)
    {
        switch (reference.RefType)
        {
            case DMReference.Type.NoRef:
                return DreamValue.Null;
            case DMReference.Type.Src:
            case DMReference.Type.Self:
                return frame.Instance != null ? new DreamValue(frame.Instance) : DreamValue.Null;
            case DMReference.Type.Usr:
                return Usr != null ? new DreamValue(Usr) : DreamValue.Null;
            case DMReference.Type.World:
                return Context.World != null ? new DreamValue(Context.World) : DreamValue.Null;
            case DMReference.Type.Args:
                {
                    if (frame.ArgsList != null) return new DreamValue(frame.ArgsList);
                    var list = new DreamList(Context.ListType);
                    for (int i = 0; i < frame.Proc.Arguments.Length; i++)
                    {
                        list.AddValue(_stack[frame.StackBase + i]);
                    }
                    frame.ArgsList = list;
                    _callStack[_callStackPtr - 1].ArgsList = list;
                    return new DreamValue(list);
                }
            case DMReference.Type.Global:
                return Context.GetGlobal(reference.Index);
            case DMReference.Type.Argument:
                if (reference.Index < 0 || reference.Index >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, 0, this);
                return _stack[frame.ArgumentBase + reference.Index];
            case DMReference.Type.Local:
                if (reference.Index < 0 || reference.Index >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, 0, this);
                return _stack[frame.LocalBase + reference.Index];
            case DMReference.Type.SrcField:
                {
                    if (frame.Instance == null) return DreamValue.Null;
                    int idx = frame.Instance.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                    return idx != -1 ? frame.Instance.GetVariableDirect(idx) : frame.Instance.GetVariable(reference.Name);
                }
            case DMReference.Type.Field:
                {
                    if (_stackPtr - 1 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during Field reference access", frame.Proc, 0, this);
                    var obj = _stack[_stackPtr - 1 - stackOffset];
                    if (obj.TryGetValue(out DreamObject? dreamObject) && dreamObject != null)
                    {
                        int idx = dreamObject.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                        return idx != -1 ? dreamObject.GetVariableDirect(idx) : dreamObject.GetVariable(reference.Name);
                    }
                    return DreamValue.Null;
                }
            case DMReference.Type.ListIndex:
                {
                    if (_stackPtr - 2 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during ListIndex reference access", frame.Proc, 0, this);
                    var index = _stack[_stackPtr - 1 - stackOffset];
                    var listValue = _stack[_stackPtr - 2 - stackOffset];
                    if (listValue.TryGetValue(out DreamObject? listObj) && listObj is DreamList list)
                    {
                        if (index.Type == DreamValueType.Float)
                        {
                            int i = (int)index.RawFloat - 1;
                            return (i >= 0 && i < list.Values.Count) ? list.Values[i] : DreamValue.Null;
                        }
                        return list.GetValue(index);
                    }
                    return DreamValue.Null;
                }
            default:
                throw new Exception($"Unsupported reference type for reading: {reference.RefType}");
        }
    }

    /// <summary>
    /// Updates the value pointed to by a reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetReferenceValue(DMReference reference, ref CallFrame frame, DreamValue value, int stackOffset = 0)
    {
        switch (reference.RefType)
        {
            case DMReference.Type.Global:
                Context.SetGlobal(reference.Index, value);
                break;
            case DMReference.Type.Argument:
                if (reference.Index < 0 || reference.Index >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, 0, this);
                _stack[frame.ArgumentBase + reference.Index] = value;
                break;
            case DMReference.Type.Local:
                if (reference.Index < 0 || reference.Index >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, 0, this);
                _stack[frame.LocalBase + reference.Index] = value;
                break;
            case DMReference.Type.SrcField:
                if (frame.Instance != null)
                {
                    int idx = frame.Instance.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                    if (idx != -1) frame.Instance.SetVariableDirect(idx, value);
                    else frame.Instance.SetVariable(reference.Name, value);
                }
                break;
            case DMReference.Type.Field:
                {
                    if (_stackPtr - 1 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during Field reference assignment", frame.Proc, 0, this);
                    var obj = _stack[_stackPtr - 1 - stackOffset];
                    if (obj.TryGetValue(out DreamObject? dreamObject) && dreamObject != null)
                    {
                        int idx = dreamObject.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                        if (idx != -1) dreamObject.SetVariableDirect(idx, value);
                        else dreamObject.SetVariable(reference.Name, value);
                    }
                }
                break;
            case DMReference.Type.ListIndex:
                {
                    if (_stackPtr - 2 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during ListIndex reference assignment", frame.Proc, 0, this);
                    var index = _stack[_stackPtr - 1 - stackOffset];
                    var listValue = _stack[_stackPtr - 2 - stackOffset];
                    if (listValue.TryGetValue(out DreamObject? listObj) && listObj is DreamList list)
                    {
                        if (index.Type == DreamValueType.Float)
                        {
                            int i = (int)index.RawFloat - 1;
                            if (i >= 0 && i < list.Values.Count)
                                list.SetValue(i, value);
                            else if (i == list.Values.Count)
                                list.AddValue(value);
                        }
                        else
                        {
                            list.SetValue(index, value);
                        }
                    }
                }
                break;
            default:
                throw new Exception($"Unsupported reference type for writing: {reference.RefType}");
        }
    }
}
