using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    private static void HandleCombine(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var l = ref state.GetLocal(idx);
                    l = l | value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var arg = ref state.GetArgument(idx);
                    arg = arg | value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context!.GetGlobal(idx);
                    state.Thread.Context.SetGlobal(idx, val | value);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue | value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleMask(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var l = ref state.GetLocal(idx);
                    l = l & value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var arg = ref state.GetArgument(idx);
                    arg = arg & value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context!.GetGlobal(idx);
                    state.Thread.Context.SetGlobal(idx, val & value);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue & value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleCreateRangeEnumerator(ref InterpreterState state)
    {
        var id = state.ReadInt32();
        var step = state.Pop();
        var end = state.Pop();
        var start = state.Pop();
        state.Thread.SetEnumerator(id, new RangeEnumerator(start.GetValueAsDouble(), end.GetValueAsDouble(), step.GetValueAsDouble()), null);
    }

    private static void HandleCreateMultidimensionalList(ref InterpreterState state)
    {
        var dimensions = state.ReadInt32();
        var sizes = new int[dimensions];
        for (int i = dimensions - 1; i >= 0; i--) sizes[i] = (int)state.Pop().GetValueAsDouble();
        // Construct multidimensional list by creating nested lists
        DreamValue currentList = CreateNestedList(state.Thread.Context!.ListType!, sizes, 0);
        state.Push(currentList);
    }

    private static DreamValue CreateNestedList(ObjectType listType, int[] sizes, int dimension)
    {
        int size = sizes[dimension];
        var list = new DreamList(listType, size);
        if (dimension < sizes.Length - 1)
        {
            for (int i = 0; i < size; i++)
            {
                list.SetValue(i, CreateNestedList(listType, sizes, dimension + 1));
            }
        }
        return new DreamValue(list);
    }

    private static void HandleError(ref InterpreterState state)
    {
        throw new ScriptRuntimeException("Bytecode error opcode encountered", state.Proc, state.PC - 1, state.Thread);
    }

    private static void HandleBrowse(ref InterpreterState state)
    {
        var options = state.Pop();
        var body = state.Pop();
        var receiver = state.Pop();
        // Client-side browsing logic would go here
    }

    private static void HandleBrowseResource(ref InterpreterState state)
    {
        var filename = state.Pop();
        var res = state.Pop();
        var receiver = state.Pop();
    }

    private static void HandleOutputControl(ref InterpreterState state)
    {
        var control = state.Pop();
        var message = state.Pop();
        var receiver = state.Pop();
    }

    private static void HandleCreateFilteredListEnumerator(ref InterpreterState state)
    {
        var id = state.ReadInt32();
        var typeId = state.ReadInt32();
        var listValue = state.Pop();
        ObjectType? filterType = (state.Thread.Context!.ObjectTypeManager != null) ? state.Thread.Context.ObjectTypeManager.GetObjectType(typeId) : null;

        if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
        {
            state.Thread.SetEnumerator(id, new FilteredEnumerator(list.Values.GetEnumerator(), filterType), list);
        }
        else
        {
            state.Thread.SetEnumerator(id, Enumerable.Empty<DreamValue>().GetEnumerator(), null);
        }
    }

    private static void HandleLink(ref InterpreterState state)
    {
        var url = state.Pop();
        var receiver = state.Pop();
    }

    private static void HandlePrompt(ref InterpreterState state)
    {
        var typeId = state.ReadInt32();
        var defaultVal = state.Pop();
        var help = state.Pop();
        var title = state.Pop();
        var message = state.Pop();
        var receiver = state.Pop();
        state.Push(DreamValue.Null); // Blocked prompt would return a future/task
    }

    private static void HandleFtp(ref InterpreterState state)
    {
        var filename = state.Pop();
        var file = state.Pop();
        var receiver = state.Pop();
    }

    private static void HandleCreateTypeEnumerator(ref InterpreterState state)
    {
        var id = state.ReadInt32();
        var typeValue = state.Pop();
        if (typeValue.Type == DreamValueType.DreamType && typeValue.TryGetValue(out ObjectType? type) && type != null)
        {
            var objects = state.Thread.Context!.GameState?.GameObjects.Values.Where(o => o.ObjectType != null && o.ObjectType.IsSubtypeOf(type)).Select(o => new DreamValue(o)).GetEnumerator();
            state.Thread.SetEnumerator(id, objects ?? Enumerable.Empty<DreamValue>().GetEnumerator(), null);
        }
        else
        {
            state.Thread.SetEnumerator(id, Enumerable.Empty<DreamValue>().GetEnumerator(), null);
        }
    }

    private static void HandleEnumerateNoAssign(ref InterpreterState state)
    {
        var id = state.ReadInt32();
        var jump = state.ReadInt32();
        var enumerator = state.Thread.GetEnumerator(id);
        if (enumerator == null || !enumerator.MoveNext()) state.PC = jump;
    }

    private static void HandleDebuggerBreakpoint(ref InterpreterState state)
    {
        System.Diagnostics.Debugger.Break();
    }

    private static void HandlePushNResources(ref InterpreterState state)
    {
        int count = state.ReadInt32();
        for (int i = 0; i < count; i++) state.Push(new DreamValue(new DreamResource("resource", state.Strings[state.ReadInt32()])));
    }

    private static void HandlePushNStrings(ref InterpreterState state)
    {
        int count = state.ReadInt32();
        for (int i = 0; i < count; i++) state.Push(new DreamValue(state.Strings[state.ReadInt32()]));
    }

    private static void HandlePushNOfStringFloats(ref InterpreterState state)
    {
        int count = state.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            state.Push(new DreamValue(state.Strings[state.ReadInt32()]));
            state.Push(new DreamValue(state.ReadDouble()));
        }
    }

    private static void HandleCreateListNFloats(ref InterpreterState state)
    {
        int count = state.ReadInt32();
        var list = new DreamList(state.Thread.Context!.ListType!, count);
        for (int i = 0; i < count; i++) list.SetValue(i, new DreamValue(state.ReadDouble()));
        state.Push(new DreamValue(list));
    }

    private static void HandleCreateListNStrings(ref InterpreterState state)
    {
        int count = state.ReadInt32();
        var list = new DreamList(state.Thread.Context!.ListType!, count);
        for (int i = 0; i < count; i++) list.SetValue(i, new DreamValue(state.Strings[state.ReadInt32()]));
        state.Push(new DreamValue(list));
    }

    private static void HandleCreateListNRefs(ref InterpreterState state)
    {
        int count = state.ReadInt32();
        var list = new DreamList(state.Thread.Context!.ListType!, count);
        for (int i = 0; i < count; i++)
        {
            var reference = state.ReadReference();
            state.Thread._stackPtr = state.StackPtr;
            list.SetValue(i, state.Thread.GetReferenceValue(reference, ref state.Frame));
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }
        state.Push(new DreamValue(list));
    }

    private static void HandleCreateListNResources(ref InterpreterState state)
    {
        int count = state.ReadInt32();
        var list = new DreamList(state.Thread.Context!.ListType!, count);
        for (int i = 0; i < count; i++) list.SetValue(i, new DreamValue(new DreamResource("resource", state.Strings[state.ReadInt32()])));
        state.Push(new DreamValue(list));
    }

    private class RangeEnumerator : IEnumerator<DreamValue>
    {
        private double _current, _start, _end, _step;
        private bool _started;
        public RangeEnumerator(double start, double end, double step) { _start = start; _end = end; _step = step; }
        public DreamValue Current => new DreamValue(_current);
        object System.Collections.IEnumerator.Current => Current;
        public bool MoveNext()
        {
            if (!_started) { _current = _start; _started = true; }
            else _current += _step;
            return _step > 0 ? _current <= _end : _current >= _end;
        }
        public void Reset() { _started = false; }
        public void Dispose() { }
    }

    private class FilteredEnumerator : IEnumerator<DreamValue>
    {
        private IEnumerator<DreamValue> _inner;
        private ObjectType? _filter;
        public FilteredEnumerator(IEnumerator<DreamValue> inner, ObjectType? filter) { _inner = inner; _filter = filter; }
        public DreamValue Current => _inner.Current;
        object System.Collections.IEnumerator.Current => Current;
        public bool MoveNext()
        {
            while (_inner.MoveNext())
            {
                if (_filter == null) return true;
                if (_inner.Current.Type == DreamValueType.DreamObject && _inner.Current.TryGetValue(out DreamObject? obj) && obj?.ObjectType != null && obj.ObjectType.IsSubtypeOf(_filter)) return true;
            }
            return false;
        }
        public void Reset() => _inner.Reset();
        public void Dispose() => _inner.Dispose();
    }
}
