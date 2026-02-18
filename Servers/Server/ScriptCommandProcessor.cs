using System;
using System.Collections.Concurrent;
using Shared;
using Shared.Interfaces;

namespace Server
{
    public class ScriptCommandProcessor : IScriptCommandProcessor
    {
        private readonly ConcurrentQueue<(string Command, Action<string> OnResult)> _commandQueue = new();
        private readonly ICommandRegistry _commandRegistry;

        public ScriptCommandProcessor(ICommandRegistry commandRegistry)
        {
            _commandRegistry = commandRegistry;
        }

        public void EnqueueCommand(string command, Action<string> onResult)
        {
            _commandQueue.Enqueue((command, onResult));
        }

        public void ProcessCommands(IScriptManager scriptManager)
        {
            while (_commandQueue.TryDequeue(out var commandInfo))
            {
                var (command, onResult) = commandInfo;

                // Try registry first
                var result = _commandRegistry.ExecuteCommand(command);

                // Fallback to script manager if not handled
                result ??= scriptManager.ExecuteCommand(command);

                onResult(result ?? "Command executed with no result.");
            }
        }
    }
}
