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

        public ProjectService(ProjectHolder projectHolder, IObjectTypeManager objectTypeManager, ToolManager toolManager, EditorContext editorContext, IUIService uiService)
        {
            _projectHolder = projectHolder;
            _objectTypeManager = objectTypeManager;
            _toolManager = toolManager;
            _editorContext = editorContext;
            _uiService = uiService;
        }

        public bool LoadProject(string projectPath)
        {
            var project = new Project(projectPath);
            _projectHolder.SetProject(project);

            // TODO: This is temporary test data. In the future, this should be loaded from the project files.
            var wall = new ObjectType(1, "wall");
            wall.DefaultProperties["SpritePath"] = "assets/wall.png";
            _objectTypeManager.RegisterObjectType(wall);
            var floor = new ObjectType(2, "floor");
            floor.DefaultProperties["SpritePath"] = "assets/floor.png";
            _objectTypeManager.RegisterObjectType(floor);

            _toolManager.SetActiveTool(_toolManager.Tools.FirstOrDefault(), _editorContext);

            _uiService.SetActiveTab(EditorTab.Scene);
            return true;
        }
    }
}
