using System;
using System.Collections.Generic;
using Shared;
using Core.VM.Runtime;

namespace Core.VM.Procs
{
    public class SpatialNativeProcProvider : INativeProcProvider
    {
        public IDictionary<string, IDreamProc> GetNativeProcs()
        {
            var procs = new Dictionary<string, IDreamProc>();

            procs["range"] = new NativeProc("range", (thread, src, args) =>
            {
                if (thread.Context.GameApi == null) return DreamValue.Null;

                int dist = 5;
                int centerX = 0, centerY = 0, centerZ = 0;

                if (args.Length >= 4)
                {
                    dist = Math.Clamp((int)args[0].GetValueAsFloat(), 0, 100);
                    centerX = (int)args[1].GetValueAsFloat();
                    centerY = (int)args[2].GetValueAsFloat();
                    centerZ = (int)args[3].GetValueAsFloat();
                }
                else
                {
                    GameObject? center = null;
                    if (args.Length >= 2)
                    {
                        dist = Math.Clamp((int)args[0].GetValueAsFloat(), 0, 100);
                        args[1].TryGetValueAsGameObject(out center);
                    }
                    else if (args.Length == 1)
                    {
                        if (!args[0].TryGetValueAsGameObject(out center))
                        {
                            dist = Math.Clamp((int)args[0].GetValueAsFloat(), 0, 100);
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

                var results = thread.Context.GameApi.StdLib.Range(dist, centerX, centerY, centerZ);
                var list = new DreamList(thread.Context.ListType);
                foreach (var obj in results) list.AddValue(new DreamValue(obj));
                return new DreamValue(list);
            });

            procs["view"] = new NativeProc("view", (thread, src, args) =>
            {
                if (thread.Context.GameApi == null) return DreamValue.Null;

                int dist = 5;
                GameObject? viewer = null;

                if (args.Length >= 2)
                {
                    dist = Math.Clamp((int)args[0].GetValueAsFloat(), 0, 100);
                    args[1].TryGetValueAsGameObject(out viewer);
                }
                else if (args.Length == 1)
                {
                    if (!args[0].TryGetValueAsGameObject(out viewer))
                    {
                        dist = Math.Clamp((int)args[0].GetValueAsFloat(), 0, 100);
                    }
                }

                viewer ??= (thread.Usr ?? src) as GameObject;
                if (viewer == null) return new DreamValue(new DreamList(thread.Context.ListType));

                var results = thread.Context.GameApi.StdLib.View(dist, viewer);
                var list = new DreamList(thread.Context.ListType);
                foreach (var obj in results) list.AddValue(new DreamValue(obj));
                return new DreamValue(list);
            });

            procs["step"] = new NativeProc("step", (thread, src, args) =>
            {
                if (thread.Context.GameApi == null || args.Length < 2) return new DreamValue(0f);
                if (args[0].TryGetValueAsGameObject(out var obj) && obj is GameObject gameObj)
                {
                    int dir = (int)args[1].GetValueAsFloat();
                    int speed = args.Length >= 3 ? (int)args[2].GetValueAsFloat() : 0;
                    return new DreamValue((float)thread.Context.GameApi.StdLib.Step(gameObj, dir, speed));
                }
                return new DreamValue(0f);
            });

            procs["step_to"] = new NativeProc("step_to", (thread, src, args) =>
            {
                if (thread.Context.GameApi == null || args.Length < 2) return new DreamValue(0f);
                if (args[0].TryGetValueAsGameObject(out var obj) && obj is GameObject gameObj &&
                    args[1].TryGetValueAsGameObject(out var target) && target is GameObject targetObj)
                {
                    int minDist = args.Length >= 3 ? (int)args[2].GetValueAsFloat() : 0;
                    int speed = args.Length >= 4 ? (int)args[3].GetValueAsFloat() : 0;
                    return new DreamValue((float)thread.Context.GameApi.StdLib.StepTo(gameObj, targetObj, minDist, speed));
                }
                return new DreamValue(0f);
            });

            return procs;
        }
    }
}
