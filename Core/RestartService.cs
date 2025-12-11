using Shared;

namespace Core
{
    public class RestartService : IRestartService
    {
        public bool IsRestartRequested { get; private set; } = false;

        public void RequestRestart()
        {
            IsRestartRequested = true;
        }

        public void Reset()
        {
            IsRestartRequested = false;
        }
    }
}
