using Shared.Enums;
using System;
using System.Runtime.CompilerServices;
using Shared;

namespace Core.VM.Runtime;

public partial class DreamThread
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(DreamValue value)
    {
        if (_callStackPtr > 0)
        {
            var currentFrame = _callStack[_callStackPtr - 1];
            _stack.Push(value, MaxStackSize, currentFrame.Proc, currentFrame.PC, this);
        }
        else
        {
            // Fallback for when there's no call frame (e.g. initial execution setup)
            _stack.EnsureCapacity(1, MaxStackSize);
            _stack.Array[_stack.Pointer++] = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureStackCapacity(int count)
    {
        _stack.EnsureCapacity(count, MaxStackSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamValue Pop()
    {
        if (_stack.Pointer <= 0) throw new ScriptRuntimeException("Stack underflow during Pop", CurrentProc, (_callStackPtr > 0 ? _callStack[_callStackPtr - 1] : default).PC, this);
        return _stack.Pop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamValue Peek()
    {
        if (_stack.Pointer <= 0) throw new ScriptRuntimeException("Stack underflow during Peek", CurrentProc, (_callStackPtr > 0 ? _callStack[_callStackPtr - 1] : default).PC, this);
        return _stack.Array[_stack.Pointer - 1];
    }

    public DreamValue Peek(int offset)
    {
        if (_stack.Pointer - offset - 1 < 0) throw new ScriptRuntimeException($"Stack underflow during Peek({offset})", CurrentProc, (_callStackPtr > 0 ? _callStack[_callStackPtr - 1] : default).PC, this);
        return _stack.Array[_stack.Pointer - offset - 1];
    }

    public void PopCount(int count)
    {
        if (_stack.Pointer < count) throw new ScriptRuntimeException($"Stack underflow during PopCount({count})", CurrentProc, (_callStackPtr > 0 ? _callStack[_callStackPtr - 1] : default).PC, this);
        _stack.Pointer -= count;
    }
}
