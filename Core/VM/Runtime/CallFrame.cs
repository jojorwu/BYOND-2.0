using Core.VM.Procs;

namespace Core.VM.Runtime
{
    public class CallFrame
    {
        public DreamProc Proc { get; }
        public int PC { get; set; }
        public int ReturnAddress { get; }
        public int StackBase { get; }

        public CallFrame(DreamProc proc, int returnAddress, int stackBase)
        {
            Proc = proc;
            PC = 0;
            ReturnAddress = returnAddress;
            StackBase = stackBase;
        }
    }
}
