using System.Collections.Generic;

namespace Shared
{
    public interface IScriptSystem
    {
        /// <summary>
        /// Инициализация системы (загрузка библиотек, компиляторов)
        /// </summary>
        void Initialize();

        /// <summary>
        /// Загрузка и выполнение всех скриптов в указанной папке
        /// </summary>
        Task LoadScripts(string rootDirectory);

        /// <summary>
        /// Вызов события (например, OnStart, OnUpdate) во всех загруженных скриптах
        /// </summary>
        void InvokeEvent(string eventName, params object[] args);

        /// <summary>
        /// Перезагрузка скриптов (Hot Reload)
        /// </summary>
        void Reload();

        /// <summary>
        /// Executes a string command.
        /// </summary>
        string? ExecuteString(string command);
    }
}
