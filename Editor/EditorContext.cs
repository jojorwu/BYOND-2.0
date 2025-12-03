using Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Editor
{
    public class Scene
    {
        public string FilePath { get; set; }
        public GameState GameState { get; }
        public bool IsDirty { get; set; } = false;

        public Scene(string filePath)
        {
            FilePath = filePath;
            GameState = new GameState();
        }
    }

    public class EditorContext
    {
        public string ProjectRoot { get; set; } = Directory.GetCurrentDirectory();
        public ObjectType? SelectedObjectType { get; set; }
        public int CurrentZLevel { get; set; } = 0;
        public Core.ServerSettings ServerSettings { get; set; } = new();
        public Core.ClientSettings ClientSettings { get; set; } = new();

        public List<Scene> OpenScenes { get; } = new();
        public int ActiveSceneIndex { get; set; } = -1;

        public Scene? GetActiveScene()
        {
            if (ActiveSceneIndex >= 0 && ActiveSceneIndex < OpenScenes.Count)
            {
                return OpenScenes[ActiveSceneIndex];
            }
            return null;
        }

        public void OpenScene(string path)
        {
            var existingScene = OpenScenes.FirstOrDefault(s => s.FilePath == path);
            if (existingScene != null)
            {
                ActiveSceneIndex = OpenScenes.IndexOf(existingScene);
                return;
            }

            var newScene = new Scene(path);
            OpenScenes.Add(newScene);
            ActiveSceneIndex = OpenScenes.Count - 1;
        }
    }
}
