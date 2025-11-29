using System.Collections.Generic;

namespace Client
{
    public class GameState
    {
        public Dictionary<int, RenderableObject> Renderables { get; set; }
        public long TickCount { get; set; }

        public GameState()
        {
            Renderables = new Dictionary<int, RenderableObject>();
        }

        public GameState Clone()
        {
            var newState = new GameState();
            newState.Renderables = new Dictionary<int, RenderableObject>(this.Renderables);
            newState.TickCount = this.TickCount;
            return newState;
        }
    }
}
