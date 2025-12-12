namespace Shared
{
    public interface IRestartService
    {
        bool IsRestartRequested { get; }
        void RequestRestart();
        void Reset();
    }
}
