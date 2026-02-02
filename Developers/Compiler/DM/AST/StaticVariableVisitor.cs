using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;

namespace DMCompiler.Compiler.DM.AST;

public class StaticVariableVisitor : DMASTVisitor {
    public readonly List<DMASTProcStatementVarDeclaration> VarDeclarations = new();

    public override void VisitVarDeclStatement(DMASTProcStatementVarDeclaration varDecl) {
        if (varDecl.IsGlobal) {
            VarDeclarations.Add(varDecl);
        }

        // Also visit the expression being assigned to the var
        base.VisitVarDeclStatement(varDecl);
    }
}
