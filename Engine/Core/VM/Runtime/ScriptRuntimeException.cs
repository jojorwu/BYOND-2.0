using System;
using System.Collections.Generic;
using System.Text;
using Shared;
using Core.VM.Procs;

namespace Core.VM.Runtime
{
    /// <summary>
    /// Exception thrown when an error occurs during script execution in the VM.
    /// Provides detailed diagnostic information about the execution state.
    /// </summary>
    public class ScriptRuntimeException : Exception
    {
        public IDreamProc? Proc { get; }
        public int PC { get; }
        private readonly CallFrame[] _capturedStack;
        private readonly int _stackDepth;
        public DreamValue? ThrownValue { get; set; }

        public ScriptRuntimeException(string message, IDreamProc? proc, int pc, DreamThread thread)
            : base(FormatMessage(message, proc, pc))
        {
            Proc = proc;
            PC = pc;
            _stackDepth = thread._callStackPtr;
            _capturedStack = new CallFrame[_stackDepth];
            Array.Copy(thread._callStack, _capturedStack, _stackDepth);
        }

        public ScriptRuntimeException(string message, IDreamProc? proc, int pc, DreamThread thread, Exception innerException)
            : base(FormatMessage(message, proc, pc), innerException)
        {
            Proc = proc;
            PC = pc;
            _stackDepth = thread._callStackPtr;
            _capturedStack = new CallFrame[_stackDepth];
            Array.Copy(thread._callStack, _capturedStack, _stackDepth);
        }

        public IEnumerable<CallFrame> CallStack => _capturedStack.Take(_stackDepth).Reverse();

        private static string FormatMessage(string message, IDreamProc? proc, int pc)
        {
            return $"{message} (at {proc?.Name ?? "unknown"}, PC: {pc})";
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Message);
            sb.AppendLine("Script stack trace:");
            for (int i = _stackDepth - 1; i >= 0; i--)
            {
                var frame = _capturedStack[i];
                sb.AppendLine($"  at {frame.Proc?.Name ?? "unknown"} (PC: {frame.PC})");
            }
            if (InnerException != null)
            {
                sb.AppendLine("Inner Exception:");
                sb.AppendLine(InnerException.ToString());
            }
            return sb.ToString();
        }
    }
}
