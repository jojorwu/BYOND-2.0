using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime
{
    public struct CallFrame
    {
        public readonly DreamProc Proc;
        public int PC;
        public readonly int StackBase;
        public readonly int ArgumentBase;
        public readonly int LocalBase;
        public readonly DreamObject? Instance;
        public readonly bool DiscardReturnValue;
        public DreamList? ArgsList;

        public CallFrame(DreamProc proc, int pc, int stackBase, DreamObject? instance, bool discardReturnValue = false)
        {
            Proc = proc;
            PC = pc;
            StackBase = stackBase;
            ArgumentBase = stackBase;
            LocalBase = stackBase + proc.Arguments.Length;
            Instance = instance;
            DiscardReturnValue = discardReturnValue;
            ArgsList = null;
        }
    }
}
