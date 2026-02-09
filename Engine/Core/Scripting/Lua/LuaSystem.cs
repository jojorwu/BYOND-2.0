using Shared;
using System;
using System.IO;
using System.Linq;
using NLua;

namespace Core.Scripting.LuaSystem
{
    public class LuaSystem : IScriptSystem, IDisposable
    {
        private Lua? _lua;
        private readonly IGameApi _gameApi;

        public LuaSystem(IGameApi gameApi)
        {
            _gameApi = gameApi;
        }

        public void Initialize()
        {
            _lua = new Lua();

            // Sandbox the environment by nullifying dangerous globals
            _lua.DoString(@"
                os = nil
                io = nil
                debug = nil
                package = nil
                require = nil
                collectgarbage = nil
                luanet = nil
                import = nil
                dofile = nil
                loadfile = nil
                load = nil
                loadstring = nil
            ");

            // Disabling CLR for security
            // _lua.LoadCLRPackage();
            _lua["Game"] = new LuaGameApi(_gameApi); // Твоя обертка
        }

        public Task LoadScripts(string rootDirectory)
        {
            if (_lua == null) return Task.CompletedTask;
            return Task.Run(() =>
            {
                var luaFiles = Directory.GetFiles(rootDirectory, "*.lua", SearchOption.AllDirectories);
                foreach (var file in luaFiles)
                {
                    try
                    {
                        _lua.DoFile(file);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Lua Error] {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            });
        }

        public void InvokeEvent(string eventName, params object[] args)
        {
            if (_lua == null) return;
            var function = _lua[eventName] as LuaFunction;
            if (function != null)
            {
                try
                {
                    function.Call(args);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Lua Error] Event {eventName}: {ex.Message}");
                }
            }
        }

        public void Reload()
        {
            _lua?.Dispose();
            _lua = null;
            Initialize();
        }

        public void Dispose()
        {
            _lua?.Dispose();
            _lua = null;
            GC.SuppressFinalize(this);
        }

        public string? ExecuteString(string command)
        {
            try
            {
                var result = _lua?.DoString(command);
                return result?.FirstOrDefault()?.ToString();
            }
            catch (Exception ex)
            {
                return $"[Lua Error] {ex.Message}";
            }
        }
    }
}
