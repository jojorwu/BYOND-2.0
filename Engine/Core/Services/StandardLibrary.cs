using System;
using Shared;
using Core.VM.Procs;
using Core.VM.Runtime;

namespace Core
{
    public static class StandardLibrary
    {
        public static void Register(IDreamVM vm)
        {
            RegisterMath(vm);
            RegisterSystem(vm);
        }

        private static void RegisterSystem(IDreamVM vm)
        {
            RegisterNativeProc(vm, "sleep", (thread, src, args) =>
            {
                if (args.Length > 0 && args[0].TryGetValue(out float seconds))
                {
                    thread.Sleep(seconds / 10.0f); // DM sleep is in deciseconds
                }
                return DreamValue.Null;
            });
        }

        private static void RegisterMath(IDreamVM vm)
        {
            RegisterNativeProc(vm, "abs", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Abs(args[0].AsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "sin", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Sin(args[0].AsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "cos", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Cos(args[0].AsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "tan", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Tan(args[0].AsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "sqrt", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Sqrt(args[0].AsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "arcsin", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.ArcSin(args[0].AsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "arccos", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.ArcCos(args[0].AsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "arctan", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.ArcTan(args[0].AsFloat())) : DreamValue.Null);
        }

        private static void RegisterNativeProc(IDreamVM vm, string name, NativeProc.NativeProcDelegate @delegate)
        {
            var nativeProc = new NativeProc(name, @delegate);
            vm.Procs[name] = nativeProc;
        }
    }
}
