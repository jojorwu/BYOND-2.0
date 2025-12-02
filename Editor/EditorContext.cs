using Core;
using Silk.NET.OpenGL;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Editor
{
    public enum FileType
    {
        Unknown,
        Map,
        Script
    }

    public record OpenedFile(string Path, FileType Type);

    public class EditorContext
    {
        public GL Gl { get; set; } = null!;
        public ObjectType? SelectedObjectType { get; set; }
        public int CurrentZLevel { get; set; } = 0;

        public List<OpenedFile> OpenFiles { get; } = new();

        public void OpenFile(string path)
        {
            if (OpenFiles.Any(f => f.Path == path))
            {
                // TODO: Focus the existing tab instead of reopening
                return;
            }

            var extension = Path.GetExtension(path).ToLowerInvariant();
            var type = extension switch
            {
                ".dmm" or ".json" => FileType.Map,
                ".lua" or ".dm" => FileType.Script,
                _ => FileType.Unknown
            };

            if (type != FileType.Unknown)
            {
                OpenFiles.Add(new OpenedFile(path, type));
            }
        }
    }
}
