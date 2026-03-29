using Shared;
using Shared.Interfaces;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Core.Scripting.CSharp
{
    public class CSharpSystem : IScriptSystem
    {
        private readonly IGameApi _gameApi;
        private readonly IScriptBridge _scriptBridge;
        private readonly List<object> _scriptInstances = new();

        public CSharpSystem(IGameApi gameApi, IScriptBridge? scriptBridge = null)
        {
            _gameApi = gameApi;
            _scriptBridge = scriptBridge ?? MockScriptBridge.Instance;
        }

        public void Initialize() { }

        public async Task LoadScripts(string rootDirectory)
        {
            _scriptInstances.Clear();
            var csFiles = Directory.GetFiles(rootDirectory, "*.cs", SearchOption.AllDirectories);

            var options = ScriptOptions.Default
                .AddReferences(Assembly.GetExecutingAssembly()) // Доступ к Core
                .AddImports("System", "System.Numerics", "Core", "Core.Graphics");

            // Глобальный объект, доступный в скриптах
            var globals = new ScriptGlobals { Game = _gameApi, Bridge = _scriptBridge };

            foreach (var file in csFiles)
            {
                try
                {
                    string code = File.ReadAllText(file);
                    // Запускаем скрипт. Если скрипт возвращает объект (например, класс), сохраняем его.
                    var state = await CSharpScript.RunAsync(code, options, globals: globals);

                    // В реальной реализации здесь можно искать классы, реализующие IGameScript
                    // Для простоты считаем, что скрипт выполняется линейно
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[C# Error] Failed to load {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        public void InvokeEvent(string eventName, params object[] args)
        {
            // Для C# скриптов сложнее вызывать методы по имени без рефлексии по сохраненным инстансам.
            // Здесь нужна более сложная логика хранения скомпилированных классов.
        }

        public void Reload()
        {
            // Очистка и повторная загрузка
        }

        public string? ExecuteString(string command)
        {
            // Not supported for C# scripts in this manner
            return null;
        }
    }

    public class ScriptGlobals
    {
        public IGameApi? Game { get; set; }
        public IScriptBridge? Bridge { get; set; }
    }
}
