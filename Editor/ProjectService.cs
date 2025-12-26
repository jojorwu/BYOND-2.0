using Core;
using Editor.UI;
using Shared;
using System.IO;
using System.Linq;

namespace Editor
{
    public class ProjectService : IProjectService
    {
        private readonly ProjectHolder _projectHolder;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly ToolManager _toolManager;
        private readonly EditorContext _editorContext;
        private readonly IUIService _uiService;
        private readonly IDreamMakerLoader _dreamMakerLoader;
        private readonly IJsonService _jsonService;

        public ProjectService(
            ProjectHolder projectHolder,
            IObjectTypeManager objectTypeManager,
            ToolManager toolManager,
            EditorContext editorContext,
            IUIService uiService,
            IDreamMakerLoader dreamMakerLoader,
            IJsonService jsonService)
        {
            _projectHolder = projectHolder;
            _objectTypeManager = objectTypeManager;
            _toolManager = toolManager;
            _editorContext = editorContext;
            _uiService = uiService;
            _dreamMakerLoader = dreamMakerLoader;
            _jsonService = jsonService;
        }

        public bool LoadProject(string projectPath)
        {
            var project = new Project(projectPath);
            _projectHolder.SetProject(project);
            _objectTypeManager.Reset();

            var compiledJsonPath = Path.Combine(project.RootPath, "project.compiled.json");
            if (File.Exists(compiledJsonPath))
            {
                var json = File.ReadAllText(compiledJsonPath);
                var compiledDream = _jsonService.DeserializePublicDreamCompiledJson(json);
                if (compiledDream != null)
                {
                    _dreamMakerLoader.Load(compiledDream);
                }
            }

            _toolManager.SetActiveTool(_toolManager.Tools.FirstOrDefault(), _editorContext);

            _uiService.SetActiveTab(EditorTab.Scene);
            return true;
        }
    }
}
