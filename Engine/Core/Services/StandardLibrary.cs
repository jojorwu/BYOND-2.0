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
            RegisterSpatial(vm);
        }

        private static void RegisterSpatial(IDreamVM vm)
        {
            RegisterNativeProc(vm, "range", (thread, src, args) =>
            {
                if (vm.GameApi == null) return DreamValue.Null;

                int dist = 5;
                int centerX = 0, centerY = 0, centerZ = 0;

                if (args.Length >= 4)
                {
                    dist = (int)args[0].GetValueAsFloat();
                    centerX = (int)args[1].GetValueAsFloat();
                    centerY = (int)args[2].GetValueAsFloat();
                    centerZ = (int)args[3].GetValueAsFloat();
                }
                else
                {
                    GameObject? center = null;
                    if (args.Length >= 2)
                    {
                        dist = (int)args[0].GetValueAsFloat();
                        args[1].TryGetValueAsGameObject(out center);
                    }
                    else if (args.Length == 1)
                    {
                        if (!args[0].TryGetValueAsGameObject(out center))
                        {
                            dist = (int)args[0].GetValueAsFloat();
                        }
                    }

                    center ??= (thread.Usr ?? src) as GameObject;
                    if (center != null)
                    {
                        centerX = center.X;
                        centerY = center.Y;
                        centerZ = center.Z;
                    }
                }

                var results = vm.GameApi.StdLib.Range(dist, centerX, centerY, centerZ);
                var list = new DreamList(vm.ListType);
                foreach (var obj in results) list.AddValue(new DreamValue(obj));
                return new DreamValue(list);
            });

            RegisterNativeProc(vm, "view", (thread, src, args) =>
            {
                if (vm.GameApi == null) return DreamValue.Null;

                int dist = 5;
                GameObject? viewer = null;

                if (args.Length >= 2)
                {
                    dist = (int)args[0].GetValueAsFloat();
                    args[1].TryGetValueAsGameObject(out viewer);
                }
                else if (args.Length == 1)
                {
                    if (!args[0].TryGetValueAsGameObject(out viewer))
                    {
                        dist = (int)args[0].GetValueAsFloat();
                    }
                }

                viewer ??= (thread.Usr ?? src) as GameObject;
                if (viewer == null) return new DreamValue(new DreamList(vm.ListType));

                var results = vm.GameApi.StdLib.View(dist, viewer);
                var list = new DreamList(vm.ListType);
                foreach (var obj in results) list.AddValue(new DreamValue(obj));
                return new DreamValue(list);
            });
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
                args.Length > 0 ? new DreamValue(SharedOperations.Abs(args[0].GetValueAsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "sin", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Sin(args[0].GetValueAsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "cos", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Cos(args[0].GetValueAsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "tan", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Tan(args[0].GetValueAsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "sqrt", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.Sqrt(args[0].GetValueAsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "arcsin", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.ArcSin(args[0].GetValueAsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "arccos", (thread, src, args) =>
                args.Length > 0 ? new DreamValue(SharedOperations.ArcCos(args[0].GetValueAsFloat())) : DreamValue.Null);

            RegisterNativeProc(vm, "arctan", (thread, src, args) =>
            {
                if (args.Length >= 2)
                    return new DreamValue(SharedOperations.ArcTan(args[0].GetValueAsFloat(), args[1].GetValueAsFloat()));
                if (args.Length >= 1)
                    return new DreamValue(SharedOperations.ArcTan(args[0].GetValueAsFloat()));
                return DreamValue.Null;
            });
        }

        private static void RegisterNativeProc(IDreamVM vm, string name, NativeProc.NativeProcDelegate @delegate)
        {
            var nativeProc = new NativeProc(name, @delegate);
            vm.Procs[name] = nativeProc;
        }
    }
}
