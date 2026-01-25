using DMCompiler.Compiler;
using DMCompiler.Compiler.DMM;
using DMCompiler.Json;
using System.Collections.Generic;
using System.IO;
using DMCompiler.DM;
using DMCompiler.Compiler.DM;
using DMCompiler.Compiler.DMPreprocessor;
using Shared;
using Shared.Json;
using System.Linq;

namespace DMCompiler
{
    public class DMMParserService : IDmmParserService
    {
        public (IMapData?, ICompiledJson?) ParseDmm(List<string> dmFiles, string dmmPath)
        {
            if (!File.Exists(dmmPath))
            {
                return (null, null);
            }

            var settings = new DMCompilerSettings
            {
                Files = dmFiles,
                StoreMessages = true
            };

            var compiler = new DMCompiler();
            compiler.Compile(settings);

            if (compiler.CompilerMessages.Count > 0)
            {
                foreach (var message in compiler.CompilerMessages)
                {
                    if (message.Level == ErrorLevel.Error)
                    {
                        return (null, null); // Compilation failed
                    }
                }
            }

            var preprocessor = new DMPreprocessor(compiler, false);
            preprocessor.PreprocessFile(Path.GetDirectoryName(dmmPath) ?? string.Empty, Path.GetFileName(dmmPath), false);

            var lexer = new DMLexer(dmmPath, preprocessor);
            var parser = new DMMParser(compiler, lexer, 0);
            DreamMapJson mapJson = parser.ParseMap();

            MapData mapData = new MapData {
                MaxX = mapJson.MaxX,
                MaxY = mapJson.MaxY,
                MaxZ = mapJson.MaxZ
            };

            foreach (var block in mapJson.Blocks) {
                mapData.Blocks.Add(new Shared.Json.MapBlockJson {
                    X = block.X,
                    Y = block.Y,
                    Z = block.Z,
                    Width = block.Width,
                    Height = block.Height,
                    Cells = block.Cells
                });
            }

            foreach (var (name, cell) in mapJson.CellDefinitions) {
                var mapCell = new MapCellJson();

                if (cell.Turf != null) {
                    mapCell.Turf = new MapJsonObjectJson {
                        Type = cell.Turf.Type,
                        VarOverrides = cell.Turf.VarOverrides
                    };
                }
                if (cell.Area != null) {
                    mapCell.Area = new MapJsonObjectJson {
                        Type = cell.Area.Type,
                        VarOverrides = cell.Area.VarOverrides
                    };
                }
                foreach (var obj in cell.Objects) {
                    mapCell.Objects.Add(new MapJsonObjectJson {
                        Type = obj.Type,
                        VarOverrides = obj.VarOverrides
                    });
                }

                mapData.CellDefinitions.Add(name, mapCell);
            }

            var compiledJson = compiler.CreateDreamCompiledJson(new List<DreamMapJson> { mapJson }, null);

            return (mapData, compiledJson);
        }
    }
}
