using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace DMCompiler.Compiler.DM.AST;

public abstract class DMASTVisitor {
    public void Visit(DMASTNode? node) {
        node?.Visit(this);
    }

    public virtual void VisitFile(DMASTFile file) {
        Visit(file.BlockInner);
    }

    public virtual void VisitBlockInner(DMASTBlockInner block) {
        foreach (var statement in block.Statements) {
            Visit(statement);
        }
    }

    public virtual void VisitProcBlockInner(DMASTProcBlockInner block) {
        foreach (var statement in block.Statements) {
            Visit(statement);
        }
    }

    public virtual void VisitObjectDefinition(DMASTObjectDefinition objectDefinition) {
        Visit(objectDefinition.InnerBlock);
    }

    public virtual void VisitProcDefinition(DMASTProcDefinition procDefinition) {
        foreach (var parameter in procDefinition.Parameters) {
            Visit(parameter);
        }

        Visit(procDefinition.Body);
    }

    public virtual void VisitDefinitionParameter(DMASTDefinitionParameter parameter) {
        Visit(parameter.Value);
        Visit(parameter.PossibleValues);
    }

    public virtual void VisitObjectVarDefinition(DMASTObjectVarDefinition varDefinition) {
        Visit(varDefinition.Value);
    }

    public virtual void VisitMultipleObjectVarDefinitions(DMASTMultipleObjectVarDefinitions multipleVarDefinitions) {
        foreach (var varDefinition in multipleVarDefinitions.VarDefinitions) {
            Visit(varDefinition);
        }
    }

    public virtual void VisitObjectVarOverride(DMASTObjectVarOverride varOverride) {
        Visit(varOverride.Value);
    }

    #region Statements
    public virtual void VisitInvalidStatement(DMASTInvalidStatement statement) { }
    public virtual void VisitNullStatement(DMASTNullStatement statement) { }

    public virtual void VisitProcStatementExpression(DMASTProcStatementExpression statement) {
        Visit(statement.Expression);
    }

    public virtual void VisitVarDeclStatement(DMASTProcStatementVarDeclaration varDecl) {
        Visit(varDecl.Value);
    }

    public virtual void VisitProcStatementReturn(DMASTProcStatementReturn statement) {
        Visit(statement.Value);
    }

    public virtual void VisitProcStatementBreak(DMASTProcStatementBreak statement) { }
    public virtual void VisitProcStatementContinue(DMASTProcStatementContinue statement) { }

    public virtual void VisitProcStatementGoto(DMASTProcStatementGoto statement) { }
    public virtual void VisitProcStatementLabel(DMASTProcStatementLabel statement) {
        Visit(statement.Body);
    }

    public virtual void VisitProcStatementDel(DMASTProcStatementDel statement) {
        Visit(statement.Value);
    }

    public virtual void VisitProcStatementSet(DMASTProcStatementSet statement) {
        Visit(statement.Value);
    }

    public virtual void VisitProcStatementSpawn(DMASTProcStatementSpawn statement) {
        Visit(statement.Delay);
        Visit(statement.Body);
    }

    public virtual void VisitProcStatementIf(DMASTProcStatementIf statement) {
        Visit(statement.Condition);
        Visit(statement.Body);
        Visit(statement.ElseBody);
    }

    public virtual void VisitProcStatementFor(DMASTProcStatementFor statement) {
        Visit(statement.Expression1);
        Visit(statement.Expression2);
        Visit(statement.Expression3);
        Visit(statement.Body);
    }

    public virtual void VisitProcStatementInfLoop(DMASTProcStatementInfLoop statement) {
        Visit(statement.Body);
    }

    public virtual void VisitProcStatementWhile(DMASTProcStatementWhile statement) {
        Visit(statement.Conditional);
        Visit(statement.Body);
    }

    public virtual void VisitProcStatementDoWhile(DMASTProcStatementDoWhile statement) {
        Visit(statement.Conditional);
        Visit(statement.Body);
    }

    public virtual void VisitProcStatementSwitch(DMASTProcStatementSwitch statement) {
        Visit(statement.Value);
        foreach (var switchCase in statement.Cases) {
            if (switchCase is DMASTProcStatementSwitch.SwitchCaseValues values) {
                foreach (var value in values.Values) {
                    Visit(value);
                }
            }

            Visit(switchCase.Body);
        }
    }

    public virtual void VisitProcStatementBrowse(DMASTProcStatementBrowse statement) {
        Visit(statement.Receiver);
        Visit(statement.Body);
        Visit(statement.Options);
    }

    public virtual void VisitProcStatementBrowseResource(DMASTProcStatementBrowseResource statement) {
        Visit(statement.Receiver);
        Visit(statement.File);
        Visit(statement.Filename);
    }

    public virtual void VisitProcStatementOutputControl(DMASTProcStatementOutputControl statement) {
        Visit(statement.Receiver);
        Visit(statement.Message);
        Visit(statement.Control);
    }

    public virtual void VisitProcStatementLink(DMASTProcStatementLink statement) {
        Visit(statement.Receiver);
        Visit(statement.Url);
    }

    public virtual void VisitProcStatementFtp(DMASTProcStatementFtp statement) {
        Visit(statement.Receiver);
        Visit(statement.File);
        Visit(statement.Name);
    }

    public virtual void VisitProcStatementOutput(DMASTProcStatementOutput statement) {
        Visit(statement.A);
        Visit(statement.B);
    }

    public virtual void VisitProcStatementInput(DMASTProcStatementInput statement) {
        Visit(statement.A);
        Visit(statement.B);
    }

    public virtual void VisitProcStatementTryCatch(DMASTProcStatementTryCatch statement) {
        Visit(statement.TryBody);
        Visit(statement.CatchBody);
        Visit(statement.CatchParameter);
    }

    public virtual void VisitProcStatementThrow(DMASTProcStatementThrow statement) {
        Visit(statement.Value);
    }

    public virtual void VisitInvalidProcStatement(DMASTInvalidProcStatement statement) { }
    public virtual void VisitNullProcStatement(DMASTNullProcStatement statement) { }
    #endregion

    #region Expressions
    public virtual void VisitIdentifier(DMASTIdentifier identifier) { }
    public virtual void VisitNull(DMASTConstantNull constant) { }
    public virtual void VisitInteger(DMASTConstantInteger constant) { }
    public virtual void VisitFloat(DMASTConstantFloat constant) { }
    public virtual void VisitString(DMASTConstantString constant) { }
    public virtual void VisitResource(DMASTConstantResource constant) { }
    public virtual void VisitPath(DMASTPath path) { }
    public virtual void VisitConstantPath(DMASTConstantPath path) {
        Visit(path.Value);
    }

    public virtual void VisitUpwardPathSearch(DMASTUpwardPathSearch pathSearch) {
        Visit(pathSearch.Path);
        Visit(pathSearch.Search);
    }

    public virtual void VisitCallParameter(DMASTCallParameter parameter) {
        Visit(parameter.Key);
        Visit(parameter.Value);
    }

    public virtual void VisitSwitchCaseRange(DMASTSwitchCaseRange range) {
        Visit(range.RangeStart);
        Visit(range.RangeEnd);
    }

    public virtual void VisitStringFormat(DMASTStringFormat stringFormat) {
        foreach (var value in stringFormat.InterpolatedValues) {
            Visit(value);
        }
    }

    public virtual void VisitList(DMASTList list) {
        foreach (var value in list.Values) {
            Visit(value);
        }
    }

    public virtual void VisitDimensionalList(DMASTDimensionalList list) {
        foreach (var size in list.Sizes) {
            Visit(size);
        }
    }

    public virtual void VisitNewList(DMASTNewList newList) {
        foreach (var parameter in newList.Parameters) {
            Visit(parameter);
        }
    }

    public virtual void VisitAddText(DMASTAddText addText) {
        foreach (var parameter in addText.Parameters) {
            Visit(parameter);
        }
    }

    public virtual void VisitInput(DMASTInput input) {
        foreach (var parameter in input.Parameters) {
            Visit(parameter);
        }

        Visit(input.List);
    }

    public virtual void VisitLocateCoordinates(DMASTLocateCoordinates locate) {
        Visit(locate.X);
        Visit(locate.Y);
        Visit(locate.Z);
    }

    public virtual void VisitLocate(DMASTLocate locate) {
        Visit(locate.Expression);
        Visit(locate.Container);
    }

    public virtual void VisitGradient(DMASTGradient gradient) {
        foreach (var parameter in gradient.Parameters) {
            Visit(parameter);
        }
    }

    public virtual void VisitRgb(DMASTRgb rgb) {
        foreach (var parameter in rgb.Parameters) {
            Visit(parameter);
        }
    }

    public virtual void VisitPick(DMASTPick pick) {
        foreach (var value in pick.Values) {
            Visit(value.Weight);
            Visit(value.Value);
        }
    }

    public virtual void VisitLog(DMASTLog log) {
        Visit(log.Expression);
        Visit(log.BaseExpression);
    }

    public virtual void VisitCall(DMASTCall call) {
        foreach (var parameter in call.CallParameters) {
            Visit(parameter);
        }

        foreach (var parameter in call.ProcParameters) {
            Visit(parameter);
        }
    }

    public virtual void VisitVarDeclExpression(DMASTVarDeclExpression expression) {
        Visit(expression.DeclPath);
    }

    public virtual void VisitNewPath(DMASTNewPath newPath) {
        Visit(newPath.Path);
        if (newPath.Parameters != null) {
            foreach (var parameter in newPath.Parameters) {
                Visit(parameter);
            }
        }
    }

    public virtual void VisitNewExpr(DMASTNewExpr newExpr) {
        Visit(newExpr.Expression);
        if (newExpr.Parameters != null) {
            foreach (var parameter in newExpr.Parameters) {
                Visit(parameter);
            }
        }
    }

    public virtual void VisitNewInferred(DMASTNewInferred newInferred) {
        if (newInferred.Parameters != null) {
            foreach (var parameter in newInferred.Parameters) {
                Visit(parameter);
            }
        }
    }

    public virtual void VisitTernary(DMASTTernary ternary) {
        Visit(ternary.A);
        Visit(ternary.B);
        Visit(ternary.C);
    }

    public virtual void VisitExpressionInRange(DMASTExpressionInRange inRange) {
        Visit(inRange.Value);
        Visit(inRange.StartRange);
        Visit(inRange.EndRange);
        Visit(inRange.Step);
    }

    public virtual void VisitProcCall(DMASTProcCall procCall) {
        Visit(procCall.Callable as DMASTNode);
        foreach (var parameter in procCall.Parameters) {
            Visit(parameter);
        }
    }

    public virtual void VisitDereference(DMASTDereference dereference) {
        Visit(dereference.Expression);
        foreach (var operation in dereference.Operations) {
            if (operation is DMASTDereference.IndexOperation index) {
                Visit(index.Index);
            } else if (operation is DMASTDereference.CallOperation call) {
                foreach (var parameter in call.Parameters) {
                    Visit(parameter);
                }
            }
        }
    }

    public virtual void VisitCallableProcIdentifier(DMASTCallableProcIdentifier identifier) { }
    public virtual void VisitCallableSuper(DMASTCallableSuper super) { }
    public virtual void VisitCallableSelf(DMASTCallableSelf self) { }

    public virtual void VisitScopeIdentifier(DMASTScopeIdentifier scope) {
        Visit(scope.Expression);
        if (scope.CallArguments != null) {
            foreach (var parameter in scope.CallArguments) {
                Visit(parameter);
            }
        }
    }

    public virtual void VisitInvalidExpression(DMASTInvalidExpression expression) { }
    public virtual void VisitVoid(DMASTVoid voidd) { }

    public virtual void VisitBinary(DMASTBinary binary) {
        Visit(binary.LHS);
        Visit(binary.RHS);
    }

    public virtual void VisitUnary(DMASTUnary unary) {
        Visit(unary.Value);
    }
    #endregion
}
