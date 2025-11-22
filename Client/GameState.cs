using System.Collections.Generic;
using Core.Graphics;

namespace Client
{
    public class GameState
    {
        public Dictionary<int, RenderableComponent> Renderables { get; set; }
        public long TickCount { get; set; }

        public GameState()
        {
            Renderables = new Dictionary<int, RenderableComponent>();
        }

        public GameState Clone()
        {
            var newState = new GameState();
            newState.Renderables = new Dictionary<int, RenderableComponent>(this.Renderables);
            newState.TickCount = this.TickCount;
            return newState;
        }
    }
}
