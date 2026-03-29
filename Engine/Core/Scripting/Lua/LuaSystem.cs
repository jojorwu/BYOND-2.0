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
        private readonly IScriptBridge _scriptBridge;

        public LuaSystem(IGameApi gameApi, IScriptBridge? scriptBridge = null)
        {
            _gameApi = gameApi;
            _scriptBridge = scriptBridge ?? MockScriptBridge.Instance;
        }

        public void Initialize()
        {
            lock (this)
            {
                if (_lua != null) return;
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
                _lua["Bridge"] = new LuaBridgeWrapper(_scriptBridge);

                // Allow registering commands and sounds from Lua
                _lua.DoString(@"
                    function RegisterCommand(name, description, help, func)
                        Game:InternalRegisterCommand(name, description, help, func)
                    end
                    function RegisterSound(name, filePath, volume, pitch, falloff)
                        Game:RegisterSound(name, filePath, volume or 100, pitch or 1, falloff or 1)
                    end
                ");
            }
        }

        public Task LoadScripts(string rootDirectory)
        {
            if (_lua == null) return Task.CompletedTask;

            var luaFiles = Directory.GetFiles(rootDirectory, "*.lua", SearchOption.AllDirectories);
            foreach (var file in luaFiles)
            {
                try
                {
                    lock (this)
                    {
                        _lua.DoFile(file);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Lua Error] {Path.GetFileName(file)}: {ex.Message}");
                }
            }
            return Task.CompletedTask;
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
            lock (this)
            {
                _lua?.Dispose();
                _lua = null;
                Initialize();
            }
        }

        public void Dispose()
        {
            _lua?.Dispose();
            _lua = null;
            GC.SuppressFinalize(this);
        }

        public string? ExecuteString(string command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

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

        private class LuaScriptFunction : IScriptFunction
        {
            public string Name { get; }
            public ScriptLanguage Language => ScriptLanguage.Lua;
            private readonly LuaFunction _function;

            public LuaScriptFunction(string name, LuaFunction function)
            {
                Name = name;
                _function = function;
            }

            public ValueTask<object?> InvokeAsync(params object?[] args)
            {
                var result = _function.Call(args);
                return new ValueTask<object?>(result?.FirstOrDefault());
            }
        }

        private class LuaBridgeWrapper
        {
            private readonly IScriptBridge _bridge;
            public LuaBridgeWrapper(IScriptBridge bridge) => _bridge = bridge;

            public void Register(string name, LuaFunction function)
            {
                _bridge.RegisterFunction(new LuaScriptFunction(name, function));
            }

            public object? Call(string name, params object?[] args)
            {
                // Synchronous wrapper for Lua
                return _bridge.CallAsync(name, args).GetAwaiter().GetResult();
            }
        }
    }
}
