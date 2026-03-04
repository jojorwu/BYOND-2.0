using Shared.Interfaces;

namespace Shared.Interfaces;
    public interface IEngineUpdateListener
    {
        void OnStateChanged(IGameObject obj);
        void OnPositionChanged(IGameObject obj, long oldX, long oldY, long oldZ);
    }
