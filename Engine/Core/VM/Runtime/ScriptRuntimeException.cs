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
        public IDreamProc Proc { get; }
        public int PC { get; }
        public IReadOnlyList<CallFrame> CallStack { get; }

        public ScriptRuntimeException(string message, IDreamProc proc, int pc, IEnumerable<CallFrame> callStack)
            : base(FormatMessage(message, proc, pc))
        {
            Proc = proc;
            PC = pc;
            CallStack = new List<CallFrame>(callStack).AsReadOnly();
        }

        public ScriptRuntimeException(string message, IDreamProc proc, int pc, IEnumerable<CallFrame> callStack, Exception innerException)
            : base(FormatMessage(message, proc, pc), innerException)
        {
            Proc = proc;
            PC = pc;
            CallStack = new List<CallFrame>(callStack).AsReadOnly();
        }

        private static string FormatMessage(string message, IDreamProc proc, int pc)
        {
            return $"{message} (at {proc.Name}, PC: {pc})";
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Message);
            sb.AppendLine("Script stack trace:");
            foreach (var frame in CallStack)
            {
                sb.AppendLine($"  at {frame.Proc.Name} (PC: {frame.PC})");
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
