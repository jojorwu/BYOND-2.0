using Core.VM.Procs;

namespace Core.VM.Runtime
{
    public class CallFrame
    {
        public DreamProc Proc { get; }
        public int PC; // Program Counter
        public int StackBase { get; }

        public CallFrame(DreamProc proc, int stackBase)
        {
            Proc = proc;
            PC = 0;
            StackBase = stackBase;
        }
    }
}
