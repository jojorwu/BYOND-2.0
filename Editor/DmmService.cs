using System;
using System.IO;
using System.Linq;
using DMCompiler;
using DMCompiler.Json;

namespace Core
{
    public class DmmService
    {
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly Project _project;

        public DmmService(ObjectTypeManager objectTypeManager, Project project)
        {
            _objectTypeManager = objectTypeManager;
            _project = project;
        }

        public Map? LoadDmm(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("DMM file not found.", filePath);
            }

            var dmFiles = _project.GetDmFiles();
            var parserService = new DMMParserService();
            var (publicDreamMapJson, compiledJson) = parserService.ParseDmm(dmFiles, filePath);

            if (publicDreamMapJson == null || compiledJson == null)
            {
                return null;
            }

            var dreamMakerLoader = new DreamMakerLoader(_objectTypeManager, _project);
            dreamMakerLoader.Load(compiledJson);

            var typeIdMap = compiledJson.Types.Select((t, i) => new { t, i })
                .ToDictionary(x => x.i, x => _objectTypeManager.GetObjectType(x.t.Path))
                .Where(x => x.Value != null)
                .ToDictionary(x => x.Key, x => x.Value!);

            var dmmLoader = new DmmLoader(_objectTypeManager, typeIdMap);
            var map = dmmLoader.LoadMap(publicDreamMapJson);

            return map;
        }
    }
}
