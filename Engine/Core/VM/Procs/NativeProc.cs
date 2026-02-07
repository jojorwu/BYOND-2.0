using System;
using Shared;
using Core.VM.Runtime;

namespace Core.VM.Procs
{
    public class NativeProc : IDreamProc
    {
        public delegate DreamValue NativeProcDelegate(DreamThread thread, DreamObject? src, ReadOnlySpan<DreamValue> arguments);

        public string Name { get; }
        private readonly NativeProcDelegate _delegate;

        public NativeProc(string name, NativeProcDelegate @delegate)
        {
            Name = name;
            _delegate = @delegate;
        }

        public DreamValue Call(DreamThread thread, DreamObject? src, ReadOnlySpan<DreamValue> arguments)
        {
            return _delegate(thread, src, arguments);
        }
    }
}
