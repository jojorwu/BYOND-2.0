using Core;
using Editor.UI;
using Shared;
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
        private readonly ICompilerService _compilerService;
        private readonly IDreamMakerLoader _dreamMakerLoader;
        private readonly IJsonService _jsonService;

        public ProjectService(ProjectHolder projectHolder, IObjectTypeManager objectTypeManager, ToolManager toolManager, EditorContext editorContext, IUIService uiService, ICompilerService compilerService, IDreamMakerLoader dreamMakerLoader, IJsonService jsonService)
        {
            _projectHolder = projectHolder;
            _objectTypeManager = objectTypeManager;
            _toolManager = toolManager;
            _editorContext = editorContext;
            _uiService = uiService;
            _compilerService = compilerService;
            _dreamMakerLoader = dreamMakerLoader;
            _jsonService = jsonService;
        }

        public bool LoadProject(string projectPath)
        {
            var project = new Project(projectPath);
            _projectHolder.SetProject(project);

            var (compiledPath, buildMessages) = _compilerService.Compile(project.GetDmFiles());
            if (compiledPath == null)
            {
                // TODO: Show error to user
                return false;
            }

            var compiledJson = _jsonService.Load(compiledPath);
            _dreamMakerLoader.Load(compiledJson);

            _toolManager.SetActiveTool(_toolManager.Tools.FirstOrDefault(), _editorContext);

            _uiService.SetActiveTab(EditorTab.Scene);
            return true;
        }
    }
}
