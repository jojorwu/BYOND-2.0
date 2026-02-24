using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime
{
    public struct CallFrame
    {
        public DreamProc Proc { get; }
        public int PC { get; set; }
        public int StackBase { get; }
        public int ArgumentBase { get; }
        public int LocalBase { get; }
        public DreamObject? Instance { get; }
        public bool DiscardReturnValue { get; }

        public CallFrame(DreamProc proc, int pc, int stackBase, DreamObject? instance, bool discardReturnValue = false)
        {
            Proc = proc;
            PC = pc;
            StackBase = stackBase;
            ArgumentBase = stackBase;
            LocalBase = stackBase + proc.Arguments.Length;
            Instance = instance;
            DiscardReturnValue = discardReturnValue;
        }
    }
}
