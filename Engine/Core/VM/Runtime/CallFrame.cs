using Core.VM.Procs;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

namespace Core.VM.Runtime
{
    public struct CallFrame
    {
        public DreamProc Proc { get; }
        public int PC { get; set; }
        public int StackBase { get; }
        public DreamObject? Instance { get; }

        public CallFrame(DreamProc proc, int pc, int stackBase, DreamObject? instance)
        {
            Proc = proc;
            PC = pc;
            StackBase = stackBase;
            Instance = instance;
        }
    }
}
