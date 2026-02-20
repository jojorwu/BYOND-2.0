using System;
using System.Collections.Concurrent;
using Shared;

namespace Server
{
    public class ScriptCommandProcessor : IScriptCommandProcessor
    {
        private readonly ConcurrentQueue<(string Command, Action<string> OnResult)> _commandQueue = new();

        public void EnqueueCommand(string command, Action<string> onResult)
        {
            _commandQueue.Enqueue((command, onResult));
        }

        public void ProcessCommands(IScriptManager scriptManager)
        {
            while (_commandQueue.TryDequeue(out var commandInfo))
            {
                var (command, onResult) = commandInfo;
                var result = scriptManager.ExecuteCommand(command);
                onResult(result ?? "Command executed with no result.");
            }
        }
    }
}
