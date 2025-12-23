using System;

namespace Core.VM.Runtime
{
    public partial class DreamThread
    {
        private void Opcode_Sleep()
        {
            var delay = Pop();
            if (!delay.TryGetValue(out float duration))
            {
                duration = 1; // Default to 1 decisecond
            }

            if (duration < 0)
            {
                SleepUntil = DateTime.MaxValue;
            }
            else
            {
                SleepUntil = DateTime.Now.AddMilliseconds(duration * 100);
            }

            State = DreamThreadState.Sleeping;
        }
    }
}
