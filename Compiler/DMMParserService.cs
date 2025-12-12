using DMCompiler.Compiler;
using DMCompiler.Compiler.DMM;
using DMCompiler.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DMCompiler.DM;
using DMCompiler.Compiler.DM;
using DMCompiler.Compiler.DMPreprocessor;
using System.Linq;

namespace DMCompiler
{
    public class DMMParserService
    {
        public async Task<(PublicDreamMapJson? MapJson, PublicDreamCompiledJson? CompiledJson)> ParseDmmAsync(List<string> dmFiles, string dmmPath)
        {
            if (!File.Exists(dmmPath)) // Keep sync check for early exit
            {
                return (null, null);
            }

            // The compilation and parsing logic is CPU-bound and not easily async.
            // Run it on a background thread to avoid blocking the caller.
            return await Task.Run(() => {
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
                            return ((PublicDreamMapJson?)null, (PublicDreamCompiledJson?)null); // Compilation failed
                        }
                    }
                }

                var preprocessor = new DMPreprocessor(compiler, false);
                preprocessor.PreprocessFile(Path.GetDirectoryName(dmmPath) ?? string.Empty, Path.GetFileName(dmmPath), false);

                var lexer = new DMLexer(dmmPath, preprocessor);
                var parser = new DMMParser(compiler, lexer, 0);
                var mapJson = parser.ParseMap();

                var publicMapJson = new PublicDreamMapJson
                {
                    MaxX = mapJson.MaxX,
                    MaxY = mapJson.MaxY,
                    MaxZ = mapJson.MaxZ,
                    Blocks = mapJson.Blocks.Select(b => new PublicMapBlockJson(b.X, b.Y, b.Z)
                    {
                        Width = b.Width,
                        Height = b.Height,
                        Cells = b.Cells
                    }).ToList(),
                    CellDefinitions = mapJson.CellDefinitions.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new PublicCellDefinitionJson(kvp.Value.Name)
                        {
                            Turf = kvp.Value.Turf != null ? new PublicMapObjectJson(kvp.Value.Turf.Type) { VarOverrides = kvp.Value.Turf.VarOverrides } : null,
                            Area = kvp.Value.Area != null ? new PublicMapObjectJson(kvp.Value.Area.Type) { VarOverrides = kvp.Value.Area.VarOverrides } : null,
                            Objects = kvp.Value.Objects.Select(o => new PublicMapObjectJson(o.Type) { VarOverrides = o.VarOverrides }).ToList()
                        })
                };

                var (types, procs) = compiler.DMObjectTree.CreateJsonRepresentation();

                var publicTypes = types.Select(t => new PublicDreamTypeJson {
                    Path = t.Path,
                    Parent = t.Parent,
                    InitProc = t.InitProc,
                    Procs = t.Procs,
                    Verbs = t.Verbs,
                    Variables = t.Variables,
                    GlobalVariables = t.GlobalVariables,
                    ConstVariables = t.ConstVariables,
                    TmpVariables = t.TmpVariables
                }).ToArray();

                var publicProcs = procs.Select(p => new PublicProcDefinitionJson {
                    OwningTypeId = p.OwningTypeId,
                    Name = p.Name,
                    Attributes = p.Attributes,
                    MaxStackSize = p.MaxStackSize,
                    Arguments = p.Arguments?.Select(a => new PublicProcArgumentJson { Name = a.Name, Type = a.Type }).ToList(),
                    Locals = p.Locals?.Select(l => new PublicLocalVariableJson { Offset = l.Offset, Remove = l.Remove, Add = l.Add }).ToList(),
                    SourceInfo = p.SourceInfo.Select(s => new PublicSourceInfoJson { Offset = s.Offset, File = s.File, Line = s.Line }).ToList(),
                    Bytecode = p.Bytecode,
                    IsVerb = p.IsVerb,
                    VerbSrc = p.VerbSrc,
                    VerbName = p.VerbName,
                    VerbCategory = p.VerbCategory,
                    VerbDesc = p.VerbDesc,
                    Invisibility = p.Invisibility
                }).ToArray();

                var compiledJson = new PublicDreamCompiledJson {
                    Strings = compiler.DMObjectTree.StringTable,
                    Types = publicTypes,
                    Procs = publicProcs,
                    Globals = new GlobalListJson {
                        GlobalCount = compiler.DMObjectTree.Globals.Count,
                        Names = compiler.DMObjectTree.Globals.Select(g => g.Name).ToList(),
                        Globals = compiler.DMObjectTree.Globals.Select((g, i) => {
                            g.TryAsJsonRepresentation(compiler, out var json);
                            return new { i, json };
                        }).Where(x => x.json != null).ToDictionary(x => x.i, x => x.json!)
                    }
                };

                return (publicMapJson, compiledJson);
            });
        }
    }
}
