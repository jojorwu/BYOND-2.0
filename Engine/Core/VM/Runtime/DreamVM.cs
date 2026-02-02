using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using System.Collections.Generic;
using System.Linq;
using Core.VM.Procs;
using System;

namespace Core.VM.Runtime
{
    public class DreamVM : IDreamVM
    {
        public DreamVMContext Context { get; } = new();
        public List<string> Strings => Context.Strings;
        public Dictionary<string, IDreamProc> Procs => Context.Procs;
        public List<DreamValue> Globals => Context.Globals;
        public ObjectType? ListType { get => Context.ListType; set => Context.ListType = value; }
        public IObjectTypeManager? ObjectTypeManager { get => Context.ObjectTypeManager; set => Context.ObjectTypeManager = value; }
        public IGameState? GameState { get => Context.GameState; set => Context.GameState = value; }

        private readonly ServerSettings _settings;

        public DreamVM(ServerSettings settings)
        {
            _settings = settings;
        }

        public DreamThread? CreateWorldNewThread()
        {
            if (Procs.TryGetValue("/world/proc/New", out var worldNewProc) && worldNewProc is DreamProc dreamProc)
            {
                return new DreamThread(dreamProc, Context, _settings.VmMaxInstructions);
            }
            Console.WriteLine("Error: /world/proc/New not found. Is the script compiled correctly?");
            return null;
        }

        public IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null)
        {
            if (Procs.TryGetValue(procName, out var proc) && proc is DreamProc dreamProc)
            {
                return new DreamThread(dreamProc, Context, _settings.VmMaxInstructions, associatedObject);
            }

            Console.WriteLine($"Warning: Could not find proc '{procName}' to create a thread.");
            return null;
        }
    }
}
