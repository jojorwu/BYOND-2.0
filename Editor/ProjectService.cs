using Core;
using Editor.UI;
using Shared;
using System.Linq;
using Core.Projects;

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

        public ProjectService(ProjectHolder projectHolder, IObjectTypeManager objectTypeManager, ToolManager toolManager, EditorContext editorContext, IUIService uiService, ICompilerService compilerService, IDreamMakerLoader dreamMakerLoader)
        {
            _projectHolder = projectHolder;
            _objectTypeManager = objectTypeManager;
            _toolManager = toolManager;
            _editorContext = editorContext;
            _uiService = uiService;
            _compilerService = compilerService;
            _dreamMakerLoader = dreamMakerLoader;
        }

        public bool LoadProject(string projectPath)
        {
            var project = new Project(projectPath);
            _projectHolder.SetProject(project);

            var (compiledJsonPath, _) = _compilerService.Compile(project.GetDmFiles());
            if (compiledJsonPath != null)
            {
                _dreamMakerLoader.Load(compiledJsonPath);
            }

            _toolManager.SetActiveTool(_toolManager.Tools.FirstOrDefault(), _editorContext);

            _uiService.SetActiveTab(EditorTab.Scene);
            return true;
        }
    }
}
