using DMCompiler.Compiler;
using DMCompiler.Compiler.DM.AST;

namespace DMCompiler.DM;

internal partial class DMCodeTree {
    private class ProcsNode() : TypeNode("proc");
    private class VerbsNode() : TypeNode("verb");

    private class ProcNode(DMCodeTree codeTree, DreamPath owner, DMASTProcDefinition procDef) : TypeNode(procDef.Name) {
        public readonly DMASTProcDefinition ProcDef = procDef;

        private string ProcName => ProcDef.Name;
        private bool IsOverride => ProcDef.IsOverride;

        private bool _defined;

        public bool TryDefineProc(DMCompiler compiler) {
            if (_defined)
                return true;
            if (!compiler.DMObjectTree.TryGetDMObject(owner, out var dmObject))
                return false;

            _defined = true;

            // The name of every proc gets a string ID.
            // BYOND assigns the ID after every string inside the proc is assigned, but we aren't replicating that here.
            compiler.DMObjectTree.AddString(ProcName);

            bool hasProc = dmObject.HasProc(ProcName);
            if (hasProc && !IsOverride && !dmObject.OwnsProc(ProcName) && !ProcDef.Location.InDMStandard) {
                compiler.Emit(WarningCode.DuplicateProcDefinition, ProcDef.Location,
                    $"Type {owner} already inherits a proc named \"{ProcName}\" and cannot redefine it");
                return true; // TODO: Maybe fallthrough since this error is a little pedantic?
            }

            DMProc proc = compiler.DMObjectTree.CreateDMProc(dmObject, ProcDef);

            if (ProcDef.IsOverride) {
                var procs = dmObject.GetProcs(ProcDef.Name);
                if (procs != null) {
                      var parent = compiler.DMObjectTree.AllProcs[procs[0]];
                      proc.IsVerb = parent.IsVerb;
                      if (proc.IsVerb) {
                          proc.VerbName = parent.VerbName;
                          proc.VerbCategory = parent.VerbCategory;
                          proc.VerbDesc = parent.VerbDesc;
                          proc.VerbSrc = parent.VerbSrc;
                      }

                      if (parent.IsFinal)
                          compiler.Emit(WarningCode.FinalOverride, ProcDef.Location,
                              $"Proc \"{ProcDef.Name}()\" is final and cannot be overridden. Final declaration: {parent.Location}");
                }
            }

            if (dmObject == compiler.DMObjectTree.Root) { // Doesn't belong to a type, this is a global proc
                if(IsOverride) {
                    compiler.Emit(WarningCode.InvalidOverride, ProcDef.Location,
                        $"Global procs cannot be overridden - '{ProcName}' override will be ignored");
                    //Continue processing the proc anyhoo, just don't add it.
                } else {
                    compiler.VerbosePrint($"Adding global proc {ProcDef.Name}() on pass {codeTree._currentPass}");
                    compiler.DMObjectTree.AddGlobalProc(proc);
                }
            } else {
                compiler.VerbosePrint($"Adding proc {ProcDef.Name}() to {dmObject.Path} on pass {codeTree._currentPass}");
                dmObject.AddProc(proc, forceFirst: ProcDef.Location.InDMStandard);
            }

            var staticVariableVisitor = new StaticVariableVisitor();
            ProcDef.Body?.Visit(staticVariableVisitor);
            foreach (var varDecl in staticVariableVisitor.VarDeclarations) {
                var procGlobalNode = new ProcGlobalVarNode(owner, proc, varDecl);
                Children.Add(procGlobalNode);
                codeTree._waitingNodes.Add(procGlobalNode);
            }

            return true;
        }

        public override string ToString() {
            return ProcName + "()";
        }
    }

    public void AddProc(DreamPath owner, DMASTProcDefinition procDef) {
        var node = GetDMObjectNode(owner);
        var procNode = new ProcNode(this, owner, procDef);

        if (procNode.ProcDef is { Name: "New", IsOverride: false })
            _newProcs[owner] = procNode; // We need to be ready to define New() as soon as the type is created

        if (procNode.ProcDef.IsVerb) {
            node.AddVerbsNode().Children.Add(procNode);
        } else {
            node.AddProcsNode().Children.Add(procNode);
        }

        _waitingNodes.Add(procNode);
    }
}
