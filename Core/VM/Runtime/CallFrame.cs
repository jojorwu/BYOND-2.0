using Core.VM.Procs;

namespace Core.VM.Runtime
{
    public struct CallFrame
    {
        public DreamProc Proc { get; }
        public int PC { get; set; }
        public int StackBase { get; }

        public CallFrame(DreamProc proc, int pc, int stackBase)
        {
            Proc = proc;
            PC = pc;
            StackBase = stackBase;
        }
    }
}
