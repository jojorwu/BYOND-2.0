using System;
using System.Threading.Tasks;
using Core.VM.Runtime;
using Shared;
using Robust.Shared.Maths;
using System.Collections.Generic;

namespace Core.VM.Runtime
{
    public partial class DreamThread
    {
        private Task? _suspendedTask;

        public void SuspendUntil(Task task)
        {
            _suspendedTask = task;
            State = DreamThreadState.Suspended;
        }

        public bool CheckSuspension()
        {
            if (State != DreamThreadState.Suspended || _suspendedTask == null) return false;

            if (_suspendedTask.IsCompleted)
            {
                if (_suspendedTask.IsFaulted)
                {
                    var ex = _suspendedTask.Exception?.InnerException ?? _suspendedTask.Exception;
                    State = DreamThreadState.Error;
                    Console.WriteLine($"Async Task Error: {ex}");
                    _suspendedTask = null;
                    return true;
                }

                var taskType = _suspendedTask.GetType();
                if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var resultProperty = taskType.GetProperty("Result");
                    var result = resultProperty?.GetValue(_suspendedTask);

                    if (result is List<Vector3l> path && Context?.ListType != null)
                    {
                        var list = new DreamList(Context.ListType, path.Count);
                        foreach (var pos in path)
                        {
                            // For now, represent positions as strings or special objects
                            // In a real implementation, we'd probably have /datum/vector or /turf
                            list.AddValue(new DreamValue($"{pos.X},{pos.Y},{pos.Z}"));
                        }
                        Push(new DreamValue(list));
                    }
                    else
                    {
                        Push(DreamValue.FromObject(result));
                    }
                }

                _suspendedTask = null;
                State = DreamThreadState.Running;
                return true;
            }

            return false;
        }
    }
}
