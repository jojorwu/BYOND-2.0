using Core.VM.Procs;

namespace Core.VM.Runtime
{
    public class CallFrame
    {
        public DreamProc Proc { get; }
        public int PC; // Program Counter

        public CallFrame(DreamProc proc)
        {
            Proc = proc;
            PC = 0;
        }
    }
}
