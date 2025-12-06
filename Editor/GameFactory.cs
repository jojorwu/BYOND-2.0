
using Shared;
using Core;

namespace Editor
{
    public static class GameFactory
    {
        public static IGame CreateGame()
        {
            return new Game();
        }
    }
}
