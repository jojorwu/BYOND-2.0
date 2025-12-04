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
        public List<string> RecentProjects { get; } = new List<string>();

        public EditorContext()
        {
            LoadRecentProjects();
        }

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

        private void LoadRecentProjects()
        {
            var path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "BYOND2.0Editor", "recent_projects.txt");
            if (File.Exists(path))
            {
                RecentProjects.AddRange(File.ReadAllLines(path));
            }
        }

        public void AddRecentProject(string path)
        {
            if (!RecentProjects.Contains(path))
            {
                RecentProjects.Insert(0, path);
                if (RecentProjects.Count > 10)
                {
                    RecentProjects.RemoveAt(RecentProjects.Count - 1);
                }
                SaveRecentProjects();
            }
        }

        private void SaveRecentProjects()
        {
            var dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "BYOND2.0Editor");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "recent_projects.txt");
            File.WriteAllLines(path, RecentProjects);
        }
    }
}
