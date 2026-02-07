using System;
using System.Collections.Generic;
using Shared;
using Core.VM.Runtime;

namespace Core.VM.Procs
{
    public class StandardNativeProcProvider : INativeProcProvider
    {
        public IDictionary<string, IDreamProc> GetNativeProcs()
        {
            var procs = new Dictionary<string, IDreamProc>();

            // System procs
            procs["sleep"] = new NativeProc("sleep", (thread, src, args) =>
            {
                if (args.Length > 0 && args[0].TryGetValue(out float seconds))
                {
                    thread.Sleep(seconds / 10.0f); // DM sleep is in deciseconds
                }
                return DreamValue.Null;
            });

            // Math procs
            procs["abs"] = new NativeProc("abs", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Abs(args[0].AsFloat())) : DreamValue.Null);

            procs["sin"] = new NativeProc("sin", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Sin(args[0].AsFloat())) : DreamValue.Null);

            procs["cos"] = new NativeProc("cos", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Cos(args[0].AsFloat())) : DreamValue.Null);

            procs["tan"] = new NativeProc("tan", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Tan(args[0].AsFloat())) : DreamValue.Null);

            procs["sqrt"] = new NativeProc("sqrt", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Sqrt(args[0].AsFloat())) : DreamValue.Null);

            procs["arcsin"] = new NativeProc("arcsin", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.ArcSin(args[0].AsFloat())) : DreamValue.Null);

            procs["arccos"] = new NativeProc("arccos", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.ArcCos(args[0].AsFloat())) : DreamValue.Null);

            procs["arctan"] = new NativeProc("arctan", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.ArcTan(args[0].AsFloat())) : DreamValue.Null);

            return procs;
        }
    }
}
