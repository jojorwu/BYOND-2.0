using System;
using System.Collections.Generic;
using Shared;
using Core.VM.Runtime;

namespace Core.VM.Procs
{
    public class MathNativeProcProvider : INativeProcProvider
    {
        private readonly IScriptBridge? _bridge;

        public MathNativeProcProvider(IScriptBridge? bridge = null)
        {
            _bridge = bridge;
        }

        public IDictionary<string, IDreamProc> GetNativeProcs()
        {
            var procs = new Dictionary<string, IDreamProc>();

            if (_bridge != null)
            {
                procs["bridge_call"] = new NativeProc("bridge_call", (thread, instance, args) =>
                {
                    if (args.Length < 1) return DreamValue.Null;
                    string funcName = args[0].StringValue;
                    object?[] bridgeArgs = new object?[args.Length - 1];
                    for (int i = 1; i < args.Length; i++) bridgeArgs[i - 1] = args[i].ToObject();

                    var result = _bridge.CallAsync(funcName, bridgeArgs).GetAwaiter().GetResult();
                    return DreamValue.FromObject(result);
                });
            }

            procs["abs"] = new NativeProc("abs", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Abs(args[0].GetValueAsFloat())) : DreamValue.Null);

            procs["sin"] = new NativeProc("sin", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Sin(args[0].GetValueAsFloat())) : DreamValue.Null);

            procs["cos"] = new NativeProc("cos", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Cos(args[0].GetValueAsFloat())) : DreamValue.Null);

            procs["tan"] = new NativeProc("tan", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Tan(args[0].GetValueAsFloat())) : DreamValue.Null);

            procs["sqrt"] = new NativeProc("sqrt", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Sqrt(args[0].GetValueAsFloat())) : DreamValue.Null);

            procs["arcsin"] = new NativeProc("arcsin", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.ArcSin(args[0].GetValueAsFloat())) : DreamValue.Null);

            procs["arccos"] = new NativeProc("arccos", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.ArcCos(args[0].GetValueAsFloat())) : DreamValue.Null);

            procs["arctan"] = new NativeProc("arctan", (thread, src, args) =>
            {
                if (args.Length >= 2)
                    return new DreamValue(SharedOperations.ArcTan(args[0].GetValueAsFloat(), args[1].GetValueAsFloat()));
                if (args.Length >= 1)
                    return new DreamValue(SharedOperations.ArcTan(args[0].GetValueAsFloat()));
                return DreamValue.Null;
            });

            return procs;
        }
    }
}
