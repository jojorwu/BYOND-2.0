using Shared.Enums;
using System;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    public DreamThreadState Run(DreamThread thread, int instructionBudget)
    {
        if (thread.State != DreamThreadState.Running)
            return thread.State;

        _vm?.OnThreadStarted();

        ref var currentFrame = ref thread._callStack[thread._callStackPtr - 1];
        var state = new InterpreterState
        {
            Thread = thread,
            Frame = ref currentFrame,
            Proc = currentFrame.Proc,
            PC = currentFrame.PC,
            StackPtr = thread._stack.Pointer,
            BytecodeArray = currentFrame.Proc.Bytecode,
            BytecodePtr = null, // Initialized in fixed block
            Strings = thread.Context!.Strings,
            Context = thread.Context,
            Procs = thread.Context.Procs,
            World = thread.Context.World
        };
        state.RefreshSpans();

        int instructionsExecutedThisTick = 0;
        long totalInstructionsExecuted = thread._totalInstructionsExecuted;
        long maxInstructions = thread._maxInstructions;

        while (thread.State == DreamThreadState.Running)
        {
            ReportTelemetry();
            try
            {
                fixed (byte* bytecodePtr = state.BytecodeArray)
                {
                    state.BytecodePtr = bytecodePtr;
                    while (thread.State == DreamThreadState.Running)
                    {
                        int remainingBudget = instructionBudget - instructionsExecutedThisTick;
                        if (remainingBudget <= 0) goto Done;

                        if (totalInstructionsExecuted > maxInstructions)
                        {
                            thread.State = DreamThreadState.Error;
                            goto Done;
                        }

                        // Optimized chunked instruction dispatch:
                        // Execute instructions in batches of 16 to reduce budget checking overhead
                        // while still maintaining responsiveness.
                        int chunk = Math.Min(remainingBudget, 16);
                        int actualExecutedInChunk = 0;
                        for (int i = 0; i < chunk; i++)
                        {
                            if (thread.State != DreamThreadState.Running) break;

                            if (state.PC >= state.BytecodeArray.Length)
                            {
                                // Handle implicit return at end of bytecode
                                thread._stackPtr = state.StackPtr;
                                thread.Push(DreamValue.Null);
                                thread.Opcode_Return(ref state.Proc, ref state.PC);
                                state.StackPtr = thread._stackPtr;

                                if (thread.State == DreamThreadState.Running && thread._callStackPtr > 0)
                                {
                                    state.Frame = ref thread._callStack[thread._callStackPtr - 1];
                                    state.Proc = state.Frame.Proc;
                                    state.PC = state.Frame.PC;
                                    state.StackPtr = thread._stackPtr;

                                    if (state.Proc.Bytecode != state.BytecodeArray)
                                    {
                                        state.BytecodeArray = state.Proc.Bytecode;
                                        state.RefreshSpans();
                                        goto RePin;
                                    }
                                    state.RefreshSpans();
                                    break; // Break for loop to re-check budget
                                }
                                goto Done;
                            }

                            instructionsExecutedThisTick++;
                            totalInstructionsExecuted++;
                            actualExecutedInChunk++;

                            byte rawOpcode = state.BytecodePtr[state.PC++];
                            var opcode = (Opcode)rawOpcode;

                            // Fast-path switch for the most critical hot opcodes.
                            // Moving less frequent logic to the dispatch table reduces method size,
                            // enabling better JIT optimization and improving CPU branch prediction.
                            switch (opcode)
                            {
                                case Opcode.PushLocal0: state.Push(state.GetLocal(0)); break;
                                case Opcode.PushLocal1: state.Push(state.GetLocal(1)); break;
                                case Opcode.PushLocal2: state.Push(state.GetLocal(2)); break;
                                case Opcode.PushLocal3: state.Push(state.GetLocal(3)); break;
                                case Opcode.PushLocal4: state.Push(state.GetLocal(4)); break;
                                case Opcode.PushLocal5: state.Push(state.GetLocal(5)); break;
                                case Opcode.PushLocal6: state.Push(state.GetLocal(6)); break;
                                case Opcode.PushLocal7: state.Push(state.GetLocal(7)); break;
                                case Opcode.PushLocal8: state.Push(state.GetLocal(8)); break;
                                case Opcode.PushLocal9: state.Push(state.GetLocal(9)); break;
                                case Opcode.PushLocal10: state.Push(state.GetLocal(10)); break;
                                case Opcode.PushLocal11: state.Push(state.GetLocal(11)); break;
                                case Opcode.PushLocal12: state.Push(state.GetLocal(12)); break;
                                case Opcode.PushLocal13: state.Push(state.GetLocal(13)); break;
                                case Opcode.PushLocal14: state.Push(state.GetLocal(14)); break;
                                case Opcode.PushLocal15: state.Push(state.GetLocal(15)); break;
                                case Opcode.AssignLocal0: state.GetLocal(0) = state.Peek(); break;
                                case Opcode.AssignLocal1: state.GetLocal(1) = state.Peek(); break;
                                case Opcode.AssignLocal2: state.GetLocal(2) = state.Peek(); break;
                                case Opcode.AssignLocal3: state.GetLocal(3) = state.Peek(); break;
                                case Opcode.AssignLocal4: state.GetLocal(4) = state.Peek(); break;
                                case Opcode.AssignLocal5: state.GetLocal(5) = state.Peek(); break;
                                case Opcode.AssignLocal6: state.GetLocal(6) = state.Peek(); break;
                                case Opcode.AssignLocal7: state.GetLocal(7) = state.Peek(); break;
                                case Opcode.AssignLocal8: state.GetLocal(8) = state.Peek(); break;
                                case Opcode.AssignLocal9: state.GetLocal(9) = state.Peek(); break;
                                case Opcode.AssignLocal10: state.GetLocal(10) = state.Peek(); break;
                                case Opcode.AssignLocal11: state.GetLocal(11) = state.Peek(); break;
                                case Opcode.AssignLocal12: state.GetLocal(12) = state.Peek(); break;
                                case Opcode.AssignLocal13: state.GetLocal(13) = state.Peek(); break;
                                case Opcode.AssignLocal14: state.GetLocal(14) = state.Peek(); break;
                                case Opcode.AssignLocal15: state.GetLocal(15) = state.Peek(); break;
                                case Opcode.PushLocal:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Push(state.GetLocal(idx));
                                    }
                                    break;
                                case Opcode.AssignLocal:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.GetLocal(idx) = state.Peek();
                                    }
                                    break;
                                case Opcode.PushFloat:
                                    state.Push(new DreamValue(*(double*)(state.BytecodePtr + state.PC)));
                                    state.PC += 8;
                                    break;
                                case Opcode.PushNull:
                                    state.Push(DreamValue.Null);
                                    break;
                                case Opcode.Pop:
                                    state.StackPtr--;
                                    break;
                                case Opcode.Add:
                                    PerformAdd(ref state);
                                    break;
                                case Opcode.Subtract:
                                    PerformSubtract(ref state);
                                    break;
                                case Opcode.CompareEquals:
                                    PerformCompareEquals(ref state);
                                    break;
                                case Opcode.Jump:
                                    state.PC = *(int*)(state.BytecodePtr + state.PC);
                                    break;
                                case Opcode.JumpIfFalse:
                                    {
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        PerformJumpIfFalse(ref state, address);
                                    }
                                    break;
                                case Opcode.Return:
                                    thread._stackPtr = state.StackPtr;
                                    thread.Opcode_Return(ref state.Proc, ref state.PC);
                                    state.StackPtr = thread._stackPtr;
                                    RecordInstructions(actualExecutedInChunk);
                                    goto FrameChanged;
                                case Opcode.ReturnNull:
                                    state.Push(DreamValue.Null);
                                    thread._stackPtr = state.StackPtr;
                                    thread.Opcode_Return(ref state.Proc, ref state.PC);
                                    state.StackPtr = thread._stackPtr;
                                    RecordInstructions(actualExecutedInChunk);
                                    goto FrameChanged;
                                case Opcode.ReturnTrue:
                                    state.Push(DreamValue.True);
                                    thread._stackPtr = state.StackPtr;
                                    thread.Opcode_Return(ref state.Proc, ref state.PC);
                                    state.StackPtr = thread._stackPtr;
                                    RecordInstructions(actualExecutedInChunk);
                                    goto FrameChanged;
                                case Opcode.ReturnFalse:
                                    state.Push(DreamValue.False);
                                    thread._stackPtr = state.StackPtr;
                                    thread.Opcode_Return(ref state.Proc, ref state.PC);
                                    state.StackPtr = thread._stackPtr;
                                    RecordInstructions(actualExecutedInChunk);
                                    goto FrameChanged;
                                default:
                                    _dispatchTable[rawOpcode](ref state);
                                    if (OpcodeMetadataCache.CanModifyCallStack(opcode))
                                    {
                                        RecordInstructions(actualExecutedInChunk);
                                        goto FrameChanged;
                                    }
                                    break;
                            }

                            continue;

                        FrameChanged:
                            if (thread.State == DreamThreadState.Running && thread._callStackPtr > 0)
                            {
                                state.Frame = ref thread._callStack[thread._callStackPtr - 1];
                                state.Proc = state.Frame.Proc;
                                state.PC = state.Frame.PC;
                                state.StackPtr = thread._stack.Pointer;
                                if (state.Proc.Bytecode != state.BytecodeArray)
                                {
                                    state.BytecodeArray = state.Proc.Bytecode;
                                    state.RefreshSpans();
                                    goto RePin;
                                }
                                state.RefreshSpans();
                            }
                            if (thread.State != DreamThreadState.Running) break;
                        }
                        RecordInstructions(actualExecutedInChunk);
                    }
                RePin:;
                }
            }
            catch (Exception e)
            {
                _vm?.OnExceptionThrown();
                thread._stackPtr = state.StackPtr;
                var runtimeException = e as ScriptRuntimeException ?? new ScriptRuntimeException("Unexpected internal error", state.Proc, state.PC, thread, e);

                if (!thread.HandleException(runtimeException))
                    break;

                if (thread._callStackPtr > 0)
                {
                    state.Frame = ref thread._callStack[thread._callStackPtr - 1];
                    state.Proc = state.Frame.Proc;
                    state.PC = state.Frame.PC;
                    state.BytecodeArray = state.Proc.Bytecode;
                    state.StackPtr = thread._stackPtr;
                    state.RefreshSpans();
                }
            }
        }

    Done:
        thread._totalInstructionsExecuted = totalInstructionsExecuted;
        if (thread._callStackPtr > 0)
        {
            thread.SavePC(state.PC);
        }

        thread._stack.Pointer = state.StackPtr;
        if (thread.State == DreamThreadState.Finished || thread.State == DreamThreadState.Error)
        {
            _vm?.OnThreadFinished(thread);
        }
        return thread.State;
    }
}
