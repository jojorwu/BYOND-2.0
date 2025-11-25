using NLua;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Core
{
    /// <summary>
    /// Manages the execution of Lua scripts.
    /// </summary>
    public sealed class Scripting : IDisposable
    {
        private Lua lua;
        private readonly object luaLock = new object();
        private readonly GameApi game;
        private readonly EditorApi? editor;
        private readonly TimeSpan _executionTimeout = TimeSpan.FromSeconds(1);
        private Stopwatch _stopwatch = new Stopwatch();
        private GCHandle _hookHandle;

        /// <summary>
        /// Initializes a new instance of the <see cref="Scripting"/> class.
        /// </summary>
        public Scripting(GameApi game, EditorApi? editor = null)
        {
            this.game = game;
            this.editor = editor;
            lua = new Lua();
            RegisterApis();
        }

        private void RegisterApis()
        {
            lua["Game"] = new LuaGameApi(game);
            if (editor != null)
            {
                lua["Editor"] = editor;
            }
        }

        private void HookCallback(IntPtr L, IntPtr ar)
        {
            if (_stopwatch.Elapsed > _executionTimeout)
            {
                NativeLua.luaL_error(L, "Script execution timed out.");
            }
        }

        public void ExecuteString(string script)
        {
            lock (luaLock)
            {
                var hookDelegate = new NativeLua.LuaHook(HookCallback);
                _hookHandle = GCHandle.Alloc(hookDelegate);

                var luaState = lua.State.Handle;

                try
                {
                    _stopwatch.Restart();
                    NativeLua.lua_sethook(luaState, hookDelegate, 8, 1000);
                    lua.DoString(script);
                }
                catch (NLua.Exceptions.LuaException ex)
                {
                    if (ex.Message.Contains("Script execution timed out."))
                    {
                        throw new Exception("Script execution timed out.");
                    }
                    throw;
                }
                finally
                {
                    NativeLua.lua_sethook(luaState, null!, 0, 0);
                    _stopwatch.Stop();
                    if (_hookHandle.IsAllocated)
                    {
                        _hookHandle.Free();
                    }
                }
            }
        }

        /// <summary>
        /// Reloads the Lua state, providing a clean environment for script execution.
        /// </summary>
        public void Reload()
        {
            lock (luaLock)
            {
                lua.Close();
                lua = new Lua();
                RegisterApis();
            }
        }

        /// <summary>
        /// Executes a Lua script from a file.
        /// </summary>
        /// <param name="filePath">The path to the script file.</param>
        /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file is not found.</exception>
        public void ExecuteFile(string? filePath)
        {
            if (filePath == null)
            {
                throw new System.ArgumentNullException(nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found.", filePath);
            }
            var script = File.ReadAllText(filePath);
            ExecuteString(script);
        }

        /// <summary>
        /// Releases the resources used by the Lua instance.
        /// </summary>
        public void Dispose()
        {
            lock (luaLock)
            {
                lua?.Dispose();
            }
        }
    }
}
