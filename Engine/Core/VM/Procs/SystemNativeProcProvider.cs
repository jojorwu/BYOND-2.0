using System;
using System.Collections.Generic;
using Shared;
using Core.VM.Runtime;

namespace Core.VM.Procs
{
    public class SystemNativeProcProvider : INativeProcProvider
    {
        private readonly ISoundApi? _soundApi;
        private readonly IScriptBridge? _bridge;

        public SystemNativeProcProvider(ISoundApi? soundApi = null, IScriptBridge? bridge = null)
        {
            _soundApi = soundApi;
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

            procs["sleep"] = new NativeProc("sleep", (thread, src, args) =>
            {
                if (args.Length > 0 && args[0].TryGetValue(out float seconds))
                {
                    if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0;
                    seconds = Math.Clamp(seconds, 0, 315360000); // Max 1 year (315360000 deciseconds)
                    thread.Sleep(seconds / 10.0f); // DM sleep is in deciseconds
                }
                return DreamValue.Null;
            });

            procs["sound"] = new NativeProc("sound", (thread, src, args) =>
            {
                if (_soundApi == null || args.Length == 0) return DreamValue.Null;

                if (args[0].TryGetValue(out string? file) && file != null)
                {
                    float volume = 100f;
                    float pitch = 1f;
                    bool repeat = false;

                    if (args.Length > 1 && args[1].TryGetValue(out double vol)) volume = (float)vol;
                    if (args.Length > 2 && args[2].TryGetValue(out double p)) pitch = (float)p;
                    if (args.Length > 3) repeat = !args[3].IsFalse();

                    if (src is GameObject obj)
                    {
                        _soundApi.PlayOn(file, obj, volume, pitch);
                    }
                    else
                    {
                        _soundApi.Play(file, volume, pitch, repeat);
                    }
                }

                return DreamValue.Null;
            });

            procs["stop_sound"] = new NativeProc("stop_sound", (thread, src, args) =>
            {
                if (_soundApi == null || args.Length == 0) return DreamValue.Null;

                if (args[0].TryGetValue(out string? file) && file != null)
                {
                    if (src is GameObject obj)
                    {
                        _soundApi.StopOn(file, obj);
                    }
                    else
                    {
                        _soundApi.Stop(file);
                    }
                }

                return DreamValue.Null;
            });

            return procs;
        }
    }
}
