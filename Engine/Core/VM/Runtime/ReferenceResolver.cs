using Shared;
using Shared.Enums;
using System.Runtime.CompilerServices;

namespace Core.VM.Runtime;

internal static class ReferenceResolver
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DreamValue GetValue(DMReference reference, DreamThread thread, ref CallFrame frame, int stackOffset = 0)
    {
        switch (reference.RefType)
        {
            case DMReference.Type.NoRef:
                return DreamValue.Null;
            case DMReference.Type.Src:
            case DMReference.Type.Self:
                return frame.Instance != null ? new DreamValue(frame.Instance) : DreamValue.Null;
            case DMReference.Type.Usr:
                return thread.Usr != null ? new DreamValue(thread.Usr) : DreamValue.Null;
            case DMReference.Type.World:
                return thread.Context.World != null ? new DreamValue(thread.Context.World) : DreamValue.Null;
            case DMReference.Type.Args:
                {
                    if (frame.ArgsList != null) return new DreamValue(frame.ArgsList);
                    var list = new DreamList(thread.Context.ListType);
                    var stack = thread._stack;
                    for (int i = 0; i < frame.Proc.Arguments.Length; i++)
                    {
                        list.AddValue(stack[frame.StackBase + i]);
                    }
                    frame.ArgsList = list;
                    return new DreamValue(list);
                }
            case DMReference.Type.Global:
                return thread.Context.GetGlobal(reference.Index);
            case DMReference.Type.Argument:
                if (reference.Index < 0 || reference.Index >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, 0, thread);
                return thread._stack[frame.ArgumentBase + reference.Index];
            case DMReference.Type.Local:
                if (reference.Index < 0 || reference.Index >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, 0, thread);
                return thread._stack[frame.LocalBase + reference.Index];
            case DMReference.Type.SrcField:
                {
                    if (frame.Instance == null) return DreamValue.Null;
                    int idx = frame.Instance.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                    return idx != -1 ? frame.Instance.GetVariableDirect(idx) : frame.Instance.GetVariable(reference.Name);
                }
            case DMReference.Type.Field:
                {
                    if (thread._stackPtr - 1 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during Field reference access", frame.Proc, 0, thread);
                    var obj = thread._stack[thread._stackPtr - 1 - stackOffset];
                    if (obj.TryGetValue(out DreamObject? dreamObject) && dreamObject != null)
                    {
                        int idx = dreamObject.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                        return idx != -1 ? dreamObject.GetVariableDirect(idx) : dreamObject.GetVariable(reference.Name);
                    }
                    return DreamValue.Null;
                }
            case DMReference.Type.ListIndex:
                {
                    if (thread._stackPtr - 2 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during ListIndex reference access", frame.Proc, 0, thread);
                    var index = thread._stack[thread._stackPtr - 1 - stackOffset];
                    var listValue = thread._stack[thread._stackPtr - 2 - stackOffset];
                    if (listValue.TryGetValue(out DreamObject? listObj) && listObj is DreamList list)
                    {
                        if (index.Type <= DreamValueType.Integer)
                        {
                            int i = (int)index.UnsafeRawDouble - 1;
                            return (i >= 0 && i < list.Values.Count) ? list.Values[i] : DreamValue.Null;
                        }
                        return list.GetValue(index);
                    }
                    return DreamValue.Null;
                }
            default:
                throw new ScriptRuntimeException($"Unsupported reference type for reading: {reference.RefType}", frame.Proc, 0, thread);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue(DMReference reference, DreamThread thread, ref CallFrame frame, DreamValue value, int stackOffset = 0)
    {
        switch (reference.RefType)
        {
            case DMReference.Type.Global:
                thread.Context.SetGlobal(reference.Index, value);
                break;
            case DMReference.Type.Argument:
                if (reference.Index < 0 || reference.Index >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, 0, thread);
                thread._stack[frame.ArgumentBase + reference.Index] = value;
                break;
            case DMReference.Type.Local:
                if (reference.Index < 0 || reference.Index >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, 0, thread);
                thread._stack[frame.LocalBase + reference.Index] = value;
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
                    if (thread._stackPtr - 1 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during Field reference assignment", frame.Proc, 0, thread);
                    var obj = thread._stack[thread._stackPtr - 1 - stackOffset];
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
                    if (thread._stackPtr - 2 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during ListIndex reference assignment", frame.Proc, 0, thread);
                    var index = thread._stack[thread._stackPtr - 1 - stackOffset];
                    var listValue = thread._stack[thread._stackPtr - 2 - stackOffset];
                    if (listValue.TryGetValue(out DreamObject? listObj) && listObj is DreamList list)
                    {
                        if (index.Type <= DreamValueType.Integer)
                        {
                            int i = (int)index.UnsafeRawDouble - 1;
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
                throw new ScriptRuntimeException($"Unsupported reference type for writing: {reference.RefType}", frame.Proc, 0, thread);
        }
    }
}
