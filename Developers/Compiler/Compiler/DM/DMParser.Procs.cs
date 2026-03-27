using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DMCompiler.Compiler.DM.AST;
using DMCompiler.DM;

namespace DMCompiler.Compiler.DM;

public partial class DMParser
{
        private DMASTProcStatement? ProcStatement() {
            var loc = Current().Location;

            if (Current().Type == TokenType.DM_Semicolon) { // A lone semicolon creates a "null statement" (like C)
                // Note that we do not consume the semicolon here
                return new DMASTNullProcStatement(loc);
            }

            var leadingColon = Check(TokenType.DM_Colon);

            DMASTExpression? expression = null;
            if (Current().Type != TokenType.DM_Var) {
                expression = Expression();
            }

            if (leadingColon && expression is not DMASTIdentifier) {
                Emit(WarningCode.BadToken, expression?.Location ?? CurrentLoc, "Expected a label identifier");
                return new DMASTInvalidProcStatement(loc);
            }

            if (expression != null) {
                switch (expression) {
                    case DMASTIdentifier identifier:
                        // This could be a sleep without parentheses
                        if (!Check(TokenType.DM_Colon) && !leadingColon && identifier.Identifier == "sleep") {
                            var procIdentifier = new DMASTCallableProcIdentifier(expression.Location, "sleep");
                            // The argument is optional
                            var sleepTime = Expression() ?? new DMASTConstantNull(Location.Internal);

                            // TODO: Make sleep an opcode
                            expression = new DMASTProcCall(expression.Location, procIdentifier,
                                new[] { new DMASTCallParameter(sleepTime.Location, sleepTime) });
                            break;
                        }

                        // But it was a label
                        return Label(identifier);
                    case DMASTRightShift rightShift:
                        // A right shift on its own becomes a special "input" statement
                        return new DMASTProcStatementInput(loc, rightShift.LHS, rightShift.RHS);
                    case DMASTLeftShift leftShift: {
                        // A left shift on its own becomes a special "output" statement
                        // Or something else depending on what's on the right ( browse(), browse_rsc(), output(), etc )
                        if (leftShift.RHS.GetUnwrapped() is DMASTProcCall {Callable: DMASTCallableProcIdentifier identifier} procCall) {
                            switch (identifier.Identifier) {
                                case "browse": {
                                    if (procCall.Parameters.Length != 1 && procCall.Parameters.Length != 2) {
                                        Emit(WarningCode.InvalidArgumentCount, procCall.Location,
                                            "browse() requires 1 or 2 parameters");
                                        return new DMASTInvalidProcStatement(procCall.Location);
                                    }

                                    DMASTExpression body = procCall.Parameters[0].Value;
                                    DMASTExpression options = (procCall.Parameters.Length == 2)
                                        ? procCall.Parameters[1].Value
                                        : new DMASTConstantNull(loc);
                                    return new DMASTProcStatementBrowse(loc, leftShift.LHS, body, options);
                                }
                                case "browse_rsc": {
                                    if (procCall.Parameters.Length != 1 && procCall.Parameters.Length != 2) {
                                        Emit(WarningCode.InvalidArgumentCount, procCall.Location,
                                            "browse_rsc() requires 1 or 2 parameters");
                                        return new DMASTInvalidProcStatement(procCall.Location);
                                    }

                                    DMASTExpression file = procCall.Parameters[0].Value;
                                    DMASTExpression filepath = (procCall.Parameters.Length == 2)
                                        ? procCall.Parameters[1].Value
                                        : new DMASTConstantNull(loc);
                                    return new DMASTProcStatementBrowseResource(loc, leftShift.LHS, file, filepath);
                                }
                                case "output": {
                                    if (procCall.Parameters.Length != 2) {
                                        Emit(WarningCode.InvalidArgumentCount, procCall.Location,
                                            "output() requires 2 parameters");
                                        return new DMASTInvalidProcStatement(procCall.Location);
                                    }

                                    DMASTExpression msg = procCall.Parameters[0].Value;
                                    DMASTExpression control = procCall.Parameters[1].Value;
                                    return new DMASTProcStatementOutputControl(loc, leftShift.LHS, msg, control);
                                }
                                case "link": {
                                    if (procCall.Parameters.Length != 1) {
                                        Emit(WarningCode.InvalidArgumentCount, procCall.Location,
                                            "link() requires 1 parameter");
                                        return new DMASTInvalidProcStatement(procCall.Location);
                                    }

                                    DMASTExpression url = procCall.Parameters[0].Value;
                                    return new DMASTProcStatementLink(loc, leftShift.LHS, url);
                                }
                                case "ftp": {
                                    if (procCall.Parameters.Length is not 1 and not 2) {
                                        Emit(WarningCode.InvalidArgumentCount, procCall.Location,
                                            "ftp() requires 1 or 2 parameters");
                                        return new DMASTInvalidProcStatement(procCall.Location);
                                    }

                                    DMASTExpression file = procCall.Parameters[0].Value;
                                    DMASTExpression name = (procCall.Parameters.Length == 2)
                                        ? procCall.Parameters[1].Value
                                        : new DMASTConstantNull(loc);
                                    return new DMASTProcStatementFtp(loc, leftShift.LHS, file, name);
                                }
                            }
                        }

                        return new DMASTProcStatementOutput(loc, leftShift.LHS, leftShift.RHS);
                    }
                }

                return new DMASTProcStatementExpression(loc, expression);
            } else {
                DMASTProcStatement? procStatement = Current().Type switch {
                    TokenType.DM_If => If(),
                    TokenType.DM_Return => Return(),
                    TokenType.DM_For => For(),
                    TokenType.DM_Set => Set(),
                    TokenType.DM_Switch => Switch(),
                    TokenType.DM_Continue => Continue(),
                    TokenType.DM_Break => Break(),
                    TokenType.DM_Spawn => Spawn(),
                    TokenType.DM_While => While(),
                    TokenType.DM_Do => DoWhile(),
                    TokenType.DM_Throw => Throw(),
                    TokenType.DM_Del => Del(),
                    TokenType.DM_Try => TryCatch(),
                    TokenType.DM_Goto => Goto(),
                    TokenType.DM_Slash or TokenType.DM_Var => ProcVarDeclaration(),
                    _ => null
                };

                if (procStatement != null) {
                    Whitespace();
                }

                return procStatement;
            }
        }

        private DMASTProcStatement? ProcVarDeclaration(bool allowMultiple = true) {
            Token firstToken = Current();
            bool wasSlash = Check(TokenType.DM_Slash);

            if (Check(TokenType.DM_Var)) {
                if (wasSlash) {
                    Emit(WarningCode.InvalidVarDefinition, "Unsupported root variable declaration");
                    // Go on to treat it as a normal var
                }

                Whitespace(); // We have to consume whitespace here since "var foo = 1" (for example) is valid DM code.
                DMASTProcStatementVarDeclaration[]? vars = ProcVarEnd(allowMultiple);
                if (vars == null) {
                    Emit(WarningCode.InvalidVarDefinition, "Expected a var declaration");
                    return new DMASTInvalidProcStatement(firstToken.Location);
                }

                foreach (var vardec in vars) {
                    if (vardec.Name is "usr" or "src" or "args" or "world" or "global" or "callee" or "caller")
                        Compiler.Emit(WarningCode.SoftReservedKeyword, vardec.Location, $"Local variable named {vardec.Name} overrides the built-in {vardec.Name} in this context.");
                }

                if (vars.Length > 1)
                    return new DMASTAggregate<DMASTProcStatementVarDeclaration>(firstToken.Location, vars);
                return vars[0];
            } else if (wasSlash) {
                ReuseToken(firstToken);
            }

            return null;
        }

        /// <summary>
        /// <see langword="WARNING:"/> This proc calls itself recursively.
        /// </summary>
        private DMASTProcStatementVarDeclaration[]? ProcVarBlock(DMASTPath? varPath) {
            Token newlineToken = Current();
            bool hasNewline = Newline();

            if (Check(TokenType.DM_Indent)) {
                List<DMASTProcStatementVarDeclaration> varDeclarations = new();

                while (!Check(TokenType.DM_Dedent)) {
                    DMASTProcStatementVarDeclaration[]? varDecl = ProcVarEnd(true, path: varPath);
                    if (varDecl != null) {
                        varDeclarations.AddRange(varDecl);
                    } else {
                        Emit(WarningCode.InvalidVarDefinition, "Expected a var declaration");
                    }

                    Whitespace();
                    Delimiter();
                    Whitespace();
                }

                return varDeclarations.ToArray();
            } else if (Check(TokenType.DM_LeftCurlyBracket)) {
                Whitespace();
                Newline();
                bool isIndented = Check(TokenType.DM_Indent);

                List<DMASTProcStatementVarDeclaration> varDeclarations = new();
                TokenType type = isIndented ? TokenType.DM_Dedent : TokenType.DM_RightCurlyBracket;
                while (!Check(type)) {
                    DMASTProcStatementVarDeclaration[]? varDecl = ProcVarEnd(true, path: varPath);
                    Delimiter();
                    Whitespace();
                    if (varDecl == null) {
                        Emit(WarningCode.InvalidVarDefinition, "Expected a var declaration");
                        continue;
                    }

                    varDeclarations.AddRange(varDecl);
                }

                if (isIndented) Consume(TokenType.DM_RightCurlyBracket, "Expected '}'");
                if (isIndented) {
                    Newline();
                    Consume(TokenType.DM_RightCurlyBracket, "Expected '}'");
                }

                return varDeclarations.ToArray();
            }
            else if (hasNewline) {
                ReuseToken(newlineToken);
            }

            return null;
        }

        private DMASTProcStatementVarDeclaration[]? ProcVarEnd(bool allowMultiple, DMASTPath? path = null) {
            var loc = Current().Location;
            DMASTPath? varPath = Path();

            if (allowMultiple) {
                DMASTProcStatementVarDeclaration[]? block = ProcVarBlock(varPath);
                if (block != null) return block;
            }

            if (varPath == null) return null;
            if (path != null) varPath = new DMASTPath(loc, path.Path.Combine(varPath.Path));

            List<DMASTProcStatementVarDeclaration> varDeclarations = new();
            while (true) {
                Whitespace();
                DMASTExpression? value = PathArray(ref varPath.Path);

                if (Check(TokenType.DM_Equals)) {
                    Whitespace();
                    value = Expression();
                    RequireExpression(ref value);
                } else if (Check(TokenType.DM_DoubleSquareBracketEquals)) {
                    Whitespace();
                    value = Expression();
                    RequireExpression(ref value);
                }

                var valType = AsComplexTypes() ?? DMValueType.Anything;

                varDeclarations.Add(new DMASTProcStatementVarDeclaration(loc, varPath, value, valType));
                if (allowMultiple && Check(TokenType.DM_Comma)) {
                    Whitespace();
                    varPath = Path();
                    if (varPath == null) {
                        Emit(WarningCode.InvalidVarDefinition, "Expected a var declaration");
                        break;
                    }
                } else {
                    break;
                }
            }

            return varDeclarations.ToArray();
        }

        /// <summary>
        /// Similar to <see cref="ProcVarBlock(DMASTPath)"/> except it handles blocks of set declarations. <br/>
        /// <see langword="TODO:"/> See if we can combine the repetitive code between this and ProcVarBlock.
        /// </summary>
        private DMASTProcStatementSet[]? ProcSetBlock() {
            Token newlineToken = Current();
            bool hasNewline = Newline();

            if (Check(TokenType.DM_Indent)) {
                List<DMASTProcStatementSet> setDeclarations = new();

                while (!Check(TokenType.DM_Dedent)) {
                    DMASTProcStatementSet[] setDecl = ProcSetEnd(false); // Repetitive nesting is a no-no here

                    setDeclarations.AddRange(setDecl);

                    Whitespace();
                    Delimiter();
                    Whitespace();
                }

                return setDeclarations.ToArray();
            } else if (Check(TokenType.DM_LeftCurlyBracket)) {
                Whitespace();
                Newline();
                bool isIndented = Check(TokenType.DM_Indent);

                List<DMASTProcStatementSet> setDeclarations = new();
                TokenType type = isIndented ? TokenType.DM_Dedent : TokenType.DM_RightCurlyBracket;
                while (!Check(type)) {
                    DMASTProcStatementSet[] setDecl = ProcSetEnd(true);
                    Delimiter();
                    Whitespace();

                    setDeclarations.AddRange(setDecl);
                }

                if (isIndented) Consume(TokenType.DM_RightCurlyBracket, "Expected '}'");
                if (isIndented) {
                    Newline();
                    Consume(TokenType.DM_RightCurlyBracket, "Expected '}'");
                }

                return setDeclarations.ToArray();
            } else if (hasNewline) {
                ReuseToken(newlineToken);
            }

            return null;
        }

        /// <param name="allowMultiple">This may look like a derelict of ProcVarEnd but it's not;<br/>
        /// Set does not allow path-based nesting of declarations the way var does, so we only allow nesting once, deactivating it thereafter.</param>
        private DMASTProcStatementSet[] ProcSetEnd(bool allowMultiple) {
            var loc = Current().Location;

            if (allowMultiple) {
                DMASTProcStatementSet[]? block = ProcSetBlock();
                if (block != null) return block;
            }

            List<DMASTProcStatementSet> setDeclarations = new(); // It's a list even in the non-block case because we could be comma-separated right mcfricking now
            while (true) { // x [in|=] y{, a [in|=] b} or something. I'm a comment, not a formal BNF expression.
                Whitespace();
                Token attributeToken = Current();
                if(!Check(TokenType.DM_Identifier)) {
                    Emit(WarningCode.BadToken, "Expected an identifier for set declaration");
                    setDeclarations.Add(new DMASTProcStatementSet(loc, "", new DMASTConstantNull(loc), false)); // prevents emitting a second error later in Set()
                    return setDeclarations.ToArray();
                }

                Whitespace();
                TokenType consumed = Consume(new[] { TokenType.DM_Equals, TokenType.DM_In },"Expected a 'in' or '=' for set declaration");
                bool wasInKeyword = (consumed == TokenType.DM_In);
                Whitespace();
                DMASTExpression? value = Expression();
                RequireExpression(ref value);
                //AsTypes(); // Intentionally not done because the 'as' keyword just kinda.. doesn't work here. I dunno.

                setDeclarations.Add(new DMASTProcStatementSet(loc, attributeToken.Text, value, wasInKeyword));
                if (!allowMultiple)
                    break;
                if (!Check(TokenType.DM_Comma))
                    break;
                Whitespace();
                // and continue!
            }

            return setDeclarations.ToArray();
        }

        private DMASTProcStatementReturn Return() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            DMASTExpression? value = Expression();

            return new DMASTProcStatementReturn(loc, value);
        }

        private DMASTProcStatementBreak? Break() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            DMASTIdentifier? label = Identifier();

            return new DMASTProcStatementBreak(loc, label);
        }

        private DMASTProcStatementContinue Continue() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            DMASTIdentifier? label = Identifier();

            return new DMASTProcStatementContinue(loc, label);
        }

        private DMASTProcStatement Goto() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            DMASTIdentifier? label = Identifier();

            if (label == null) {
                Emit(WarningCode.BadToken, "Expected a label");
                return new DMASTInvalidProcStatement(loc);
            }

            return new DMASTProcStatementGoto(loc, label);
        }

        private DMASTProcStatementDel Del() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            bool hasParenthesis = Check(TokenType.DM_LeftParenthesis);
            Whitespace();
            DMASTExpression? value = Expression();
            RequireExpression(ref value, "Expected value to delete");
            if (hasParenthesis) ConsumeRightParenthesis();

            return new DMASTProcStatementDel(loc, value);
        }

        /// <returns>Either a <see cref="DMASTProcStatementSet"/> or a DMASTAggregate that acts as a container for them. May be null.</returns>
        private DMASTProcStatement Set() {
            var loc = Current().Location;
            Advance();

            Whitespace();

            DMASTProcStatementSet[] sets = ProcSetEnd(true);
            if (sets.Length == 0) {
                Emit(WarningCode.InvalidSetStatement, "Expected set declaration");
                return new DMASTInvalidProcStatement(loc);
            }

            if (sets.Length > 1)
                return new DMASTAggregate<DMASTProcStatementSet>(loc, sets);
            return sets[0];
        }

        private DMASTProcStatementSpawn Spawn() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            bool hasArg = Check(TokenType.DM_LeftParenthesis);
            DMASTExpression? delay = null;

            if (hasArg) {
                Whitespace();

                if (!Check(TokenType.DM_RightParenthesis)) {
                    delay = Expression();
                    RequireExpression(ref delay, "Expected a delay");

                    ConsumeRightParenthesis();
                }

                Whitespace();
            }

            Newline();

            DMASTProcBlockInner? body = ProcBlock();
            if (body == null) {
                DMASTProcStatement? statement = ProcStatement();

                if (statement != null) {
                    body = new DMASTProcBlockInner(loc, statement);
                } else {
                    Emit(WarningCode.MissingBody, "Expected body or statement");
                    body = new DMASTProcBlockInner(loc);
                }
            }

            return new DMASTProcStatementSpawn(loc, delay ?? new DMASTConstantInteger(loc, 0), body);
        }

        private void ExtraColonPeriod() {
            var token = Current();
            if (token.Type is not (TokenType.DM_Colon or TokenType.DM_Period))
                return;

            Advance();

            if (Current().Type is not (TokenType.DM_Semicolon or TokenType.Newline) && !WhitespaceTypes.Contains(Current().Type)) {
                ReuseToken(token);
                return;
            }

            Emit(WarningCode.ExtraToken, token.Location, "Extra token at end of proc statement");
        }

        private DMASTProcStatementIf If() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            Consume(TokenType.DM_LeftParenthesis, "Expected '('");
            BracketWhitespace();
            DMASTExpression? condition = Expression();
            RequireExpression(ref condition, "Expected a condition");

            if (condition is DMASTAssign) {
                Emit(WarningCode.AssignmentInConditional, condition.Location, "Assignment in conditional");
            }

            BracketWhitespace();
            ConsumeRightParenthesis();
            ExtraColonPeriod();

            Whitespace();

            DMASTProcStatement? procStatement = ProcStatement();
            DMASTProcBlockInner? elseBody = null;
            DMASTProcBlockInner? body = (procStatement != null)
                ? new DMASTProcBlockInner(loc, procStatement)
                : ProcBlock();
            body ??= new DMASTProcBlockInner(loc);

            Token afterIfBody = Current();
            bool newLineAfterIf = Delimiter();
            if (newLineAfterIf) Whitespace();
            if (Check(TokenType.DM_Else)) {
                Whitespace();
                Check(TokenType.DM_Colon);
                Whitespace();
                procStatement = ProcStatement();

                elseBody = (procStatement != null)
                    ? new DMASTProcBlockInner(loc, procStatement)
                    : ProcBlock();
                elseBody ??= new DMASTProcBlockInner(loc);
            } else if (newLineAfterIf) {
                ReuseToken(afterIfBody);
            }

            return new DMASTProcStatementIf(loc, condition, body, elseBody);
        }

        private DMASTProcStatement For() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            Consume(TokenType.DM_LeftParenthesis, "Expected '('");
            Whitespace();

            if (Check(TokenType.DM_RightParenthesis)) {
                ExtraColonPeriod();

                return new DMASTProcStatementInfLoop(loc, GetForBody());
            }

            _allowVarDeclExpression = true;
            DMASTExpression? expr1 = Expression();
            DMComplexValueType? dmTypes = AsComplexTypes();
            Whitespace();
            _allowVarDeclExpression = false;
            if (expr1 == null) {
                if (!ForSeparatorTypes.Contains(Current().Type)) {
                    Emit(WarningCode.BadExpression, "Expected 1st expression in for");
                }

                expr1 = new DMASTConstantNull(loc);
            }

            if (Check(TokenType.DM_To)) {
                if (expr1 is DMASTAssign assign) {
                    ExpressionTo(out var endRange, out var step);
                    Consume(TokenType.DM_RightParenthesis, "Expected ')' in for after to expression");
                    ExtraColonPeriod();

                    return new DMASTProcStatementFor(loc, new DMASTExpressionInRange(loc, assign.LHS, assign.RHS, endRange, step), null, null, dmTypes, GetForBody());
                } else {
                    Emit(WarningCode.BadExpression, "Expected = before to in for");
                    return new DMASTInvalidProcStatement(loc);
                }
            }

            if (Check(TokenType.DM_In)) {
                Whitespace();
                DMASTExpression? listExpr = Expression();
                RequireExpression(ref listExpr);
                Whitespace();
                Consume(TokenType.DM_RightParenthesis, "Expected ')' in for after expression 2");
                ExtraColonPeriod();

                return new DMASTProcStatementFor(loc, new DMASTExpressionIn(loc, expr1, listExpr), null, null, dmTypes, GetForBody());
            }

            if (!Check(ForSeparatorTypes)) {
                Consume(TokenType.DM_RightParenthesis, "Expected ')' in for after expression 1");
                ExtraColonPeriod();

                return new DMASTProcStatementFor(loc, expr1, null, null, dmTypes, GetForBody());
            }

            if (Check(TokenType.DM_RightParenthesis)) {
                ExtraColonPeriod();

                return new DMASTProcStatementFor(loc, expr1, null, null, dmTypes, GetForBody());
            }

            Whitespace();
            DMASTExpression? expr2 = Expression();
            if (expr2 == null) {
                if (!ForSeparatorTypes.Contains(Current().Type)) {
                    Emit(WarningCode.BadExpression, "Expected 2nd expression in for");
                }

                expr2 = new DMASTConstantInteger(loc, 1);
            }

            if (!Check(ForSeparatorTypes)) {
                Consume(TokenType.DM_RightParenthesis, "Expected ')' in for after expression 2");
                ExtraColonPeriod();

                return new DMASTProcStatementFor(loc, expr1, expr2, null, dmTypes, GetForBody());
            }

            if (Check(TokenType.DM_RightParenthesis)) {
                ExtraColonPeriod();

                return new DMASTProcStatementFor(loc, expr1, expr2, null, dmTypes, GetForBody());
            }

            Whitespace();
            DMASTExpression? expr3 = Expression();
            if (expr3 == null) {
                if (Current().Type != TokenType.DM_RightParenthesis) {
                    Emit(WarningCode.BadExpression, "Expected 3nd expression in for");
                }

                expr3 = new DMASTConstantNull(loc);
            }

            Consume(TokenType.DM_RightParenthesis, "Expected ')' in for after expression 3");
            ExtraColonPeriod();

            return new DMASTProcStatementFor(loc, expr1, expr2, expr3, dmTypes, GetForBody());

            DMASTProcBlockInner GetForBody() {
                Whitespace();

                DMASTProcBlockInner? body = ProcBlock();
                if (body != null)
                    return body;

                var statement = ProcStatement();
                return statement != null
                    ? new DMASTProcBlockInner(loc, statement)
                    : new DMASTProcBlockInner(CurrentLoc);
            }
        }

        private DMASTProcStatement While() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            Consume(TokenType.DM_LeftParenthesis, "Expected '('");
            Whitespace();
            DMASTExpression? conditional = Expression();
            RequireExpression(ref conditional, "Expected a condition");
            ConsumeRightParenthesis();
            Check(TokenType.DM_Semicolon);
            Whitespace();
            DMASTProcBlockInner? body = ProcBlock();

            if (body == null) {
                DMASTProcStatement? statement = ProcStatement();

                //Loops without a body are valid DM
                statement ??= new DMASTProcStatementContinue(loc);

                body = new DMASTProcBlockInner(loc, statement);
            }

            if (conditional is DMASTConstantInteger integer && integer.Value != 0) {
                return new DMASTProcStatementInfLoop(loc, body);
            }

            return new DMASTProcStatementWhile(loc, conditional, body);
        }

        private DMASTProcStatementDoWhile DoWhile() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            DMASTProcBlockInner? body = ProcBlock();

            if (body == null) {
                DMASTProcStatement? statement = ProcStatement();
                if (statement is null) { // This is consistently fatal in BYOND
                    Emit(WarningCode.MissingBody, "Expected statement - do-while requires a non-empty block");
                    //For the sake of argument, add a statement (avoids repetitive warning emissions down the line :^) )
                    statement = new DMASTInvalidProcStatement(loc);
                }

                body = new DMASTProcBlockInner(loc, new[] { statement }, null);
            }

            Newline();
            Whitespace();
            if (!Check(TokenType.DM_While)) {
                // "do while()" (no newline) puts the 'while' in the body; the prior MissingBody check only handled it with a newline
                if(body.Statements is [DMASTProcStatementWhile]) {
                    Emit(WarningCode.MissingBody, "Expected statement - do-while requires a non-empty block");
                } else {
                    Emit(WarningCode.BadToken, "Expected 'while'");
                }

                LocateNextStatement();
                return new DMASTProcStatementDoWhile(loc, new DMASTInvalidExpression(loc), body);
            }

            Whitespace();
            Consume(TokenType.DM_LeftParenthesis, "Expected '('");
            Whitespace();
            DMASTExpression? conditional = Expression();
            RequireExpression(ref conditional, "Expected a condition");
            ConsumeRightParenthesis();
            Whitespace();

            return new DMASTProcStatementDoWhile(loc, conditional, body);
        }

        private DMASTProcStatementSwitch Switch() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            Consume(TokenType.DM_LeftParenthesis, "Expected '('");
            Whitespace();
            DMASTExpression? value = Expression();
            RequireExpression(ref value, "Switch statement is missing a value");
            ConsumeRightParenthesis();
            Whitespace();

            DMASTProcStatementSwitch.SwitchCase[]? switchCases = SwitchCases();
            if (switchCases == null) {
                switchCases = [];
                Emit(WarningCode.MissingBody, "Expected switch cases");
            }

            return new DMASTProcStatementSwitch(loc, value, switchCases);
        }

        private DMASTProcStatementSwitch.SwitchCase[]? SwitchCases() {
            Token beforeSwitchBlock = Current();
            bool hasNewline = Newline();

            DMASTProcStatementSwitch.SwitchCase[]? switchCases = BracedSwitchInner() ?? IndentedSwitchInner();

            if (switchCases == null && hasNewline) {
                ReuseToken(beforeSwitchBlock);
            }

            return switchCases;
        }

        private DMASTProcStatementSwitch.SwitchCase[]? BracedSwitchInner() {
            if (Check(TokenType.DM_LeftCurlyBracket)) {
                Whitespace();
                Newline();
                bool isIndented = Check(TokenType.DM_Indent);
                DMASTProcStatementSwitch.SwitchCase[] switchInner = SwitchInner();
                if (isIndented) Check(TokenType.DM_Dedent);
                Newline();
                Consume(TokenType.DM_RightCurlyBracket, "Expected '}'");

                return switchInner;
            }

            return null;
        }

        private DMASTProcStatementSwitch.SwitchCase[]? IndentedSwitchInner() {
            if (Check(TokenType.DM_Indent)) {
                DMASTProcStatementSwitch.SwitchCase[] switchInner = SwitchInner();
                Consume(TokenType.DM_Dedent, "Expected \"if\" or \"else\"");

                return switchInner;
            }

            return null;
        }

        private DMASTProcStatementSwitch.SwitchCase[] SwitchInner() {
            List<DMASTProcStatementSwitch.SwitchCase> switchCases = new();
            DMASTProcStatementSwitch.SwitchCase? switchCase = SwitchCase(out var empty);

            while (switchCase is not null) {
                if(!empty) // Empty switch cases (e.g. 'if()') appear to be a no-op; definitely not equivalent to 'if(null)'
                    switchCases.Add(switchCase);
                Newline();
                Whitespace();
                switchCase = SwitchCase(out empty);
            }

            return switchCases.ToArray();
        }

        private DMASTProcStatementSwitch.SwitchCase? SwitchCase(out bool empty) {
            empty = false;
            if (Check(TokenType.DM_If)) {
                List<DMASTExpression> expressions = new();

                Whitespace();
                Consume(TokenType.DM_LeftParenthesis, "Expected '('");

                do {
                    BracketWhitespace();

                    DMASTExpression? expression = Expression();
                    if (expression == null) {
                        if (expressions.Count == 0) {
                            empty = true;
                            Compiler.Emit(WarningCode.SuspiciousSwitchCase, CurrentLoc,
                                "Empty switch case will never execute");
                        }

                        break;
                    }

                    if (Check(TokenType.DM_To)) {
                        Whitespace();
                        var loc = Current().Location;
                        DMASTExpression? rangeEnd = Expression();
                        if (rangeEnd == null) {
                            Compiler.Emit(WarningCode.BadExpression, loc, "Expected an upper limit");
                            rangeEnd = new DMASTConstantNull(loc); // Fallback to null
                        }

                        expressions.Add(new DMASTSwitchCaseRange(loc, expression, rangeEnd));
                    } else {
                        expressions.Add(expression);
                    }

                    Delimiter();
                } while (Check(TokenType.DM_Comma));

                Whitespace();
                ConsumeRightParenthesis();
                Whitespace();
                DMASTProcBlockInner? body = ProcBlock();

                if (body == null) {
                    DMASTProcStatement? statement = ProcStatement();

                    body = (statement != null)
                        ? new DMASTProcBlockInner(statement.Location, statement)
                        : new DMASTProcBlockInner(CurrentLoc);
                }

                return new DMASTProcStatementSwitch.SwitchCaseValues(expressions.ToArray(), body);
            } else if (Check(TokenType.DM_Else)) {
                Whitespace();
                var loc = Current().Location;
                if (Current().Type == TokenType.DM_If) {
                    //From now on, all if/elseif/else are actually part of this if's chain, not the switch's.
                    //Ambiguous, but that is parity behaviour. Ergo, the following emission.
                    Compiler.Emit(WarningCode.SuspiciousSwitchCase, loc,
                        "Expected \"if\" or \"else\" - \"else if\" is ambiguous as a switch case and may cause unintended flow");
                }

                DMASTProcBlockInner? body = ProcBlock();

                if (body == null) {
                    DMASTProcStatement? statement = ProcStatement();

                    body = (statement != null)
                        ? new DMASTProcBlockInner(loc, statement)
                        : new DMASTProcBlockInner(loc);
                }

                return new DMASTProcStatementSwitch.SwitchCaseDefault(body);
            }

            return null;
        }

        private DMASTProcStatementTryCatch TryCatch() {
            var loc = Current().Location;
            Advance();

            Whitespace();

            DMASTProcBlockInner? tryBody = ProcBlock();
            if (tryBody == null) {
                DMASTProcStatement? statement = ProcStatement();
                if (statement == null) {
                    statement = new DMASTInvalidProcStatement(loc);
                    Emit(WarningCode.MissingBody, "Expected body or statement");
                }

                tryBody = new DMASTProcBlockInner(loc,statement);
            }

            Newline();
            Whitespace();
            Consume(TokenType.DM_Catch, "Expected catch");
            Whitespace();

            // catch(var/exception/E)
            DMASTProcStatement? parameter = null;
            if (Check(TokenType.DM_LeftParenthesis)) {
                BracketWhitespace();
                parameter = ProcVarDeclaration(allowMultiple: false);
                BracketWhitespace();
                ConsumeRightParenthesis();
                Whitespace();
            }

            DMASTProcBlockInner? catchBody = ProcBlock();
            if (catchBody == null) {
                DMASTProcStatement? statement = ProcStatement();

                if (statement != null) catchBody = new DMASTProcBlockInner(loc, statement);
            }

            return new DMASTProcStatementTryCatch(loc, tryBody, catchBody, parameter);
        }

        private DMASTProcStatementThrow Throw() {
            var loc = Current().Location;
            Advance();

            Whitespace();
            DMASTExpression? value = Expression();
            RequireExpression(ref value, "Throw statement must have a value");

            return new DMASTProcStatementThrow(loc, value);
        }

        private DMASTProcStatementLabel Label(DMASTIdentifier expression) {
            Whitespace();
            Newline();

            DMASTProcBlockInner? body = ProcBlock();

            return new DMASTProcStatementLabel(expression.Location, expression.Identifier, body);
        }

        private List<DMASTDefinitionParameter> DefinitionParameters(out bool wasIndeterminate) {
            List<DMASTDefinitionParameter> parameters = new();
            DMASTDefinitionParameter? parameter = DefinitionParameter(out wasIndeterminate);

            if (parameter != null) parameters.Add(parameter);

            BracketWhitespace();

            while (Check(TokenType.DM_Comma)) {
                BracketWhitespace();
                parameter = DefinitionParameter(out wasIndeterminate);

                if (parameter != null) {
                    parameters.Add(parameter);
                    BracketWhitespace();
                }

                if (Check(TokenType.DM_Null)) {
                    // Breaking change - BYOND creates a var named null that overrides the keyword. No error.
                    if (Emit(WarningCode.SoftReservedKeyword, "'null' is not a valid variable name")) { // If it's an error, skip over this var instantiation.
                        Advance();
                        BracketWhitespace();
                        Check(TokenType.DM_Comma);
                        BracketWhitespace();
                        parameters.AddRange(DefinitionParameters(out wasIndeterminate));
                    }
                }
            }

            return parameters;
        }

        private DMASTDefinitionParameter? DefinitionParameter(out bool wasIndeterminate) {
            DMASTPath? path = Path();

            if (path != null) {
                if (path.Path.PathString.StartsWith("/var/")) {
                    Emit(WarningCode.ProcArgumentGlobal, $"Proc argument \"{path.Path.PathString}\" starting with \"/var/\" will create a global variable. Replace with \"{path.Path.PathString[1..]}\"");
                }

                var loc = Current().Location;
                Whitespace();

                PathArray(ref path.Path);

                if (path.Path.LastElement is "usr" or "src" or "args" or "world" or "global" or "callee" or "caller")
                    Compiler.Emit(WarningCode.SoftReservedKeyword, loc, $"Proc parameter named {path.Path.LastElement} overrides the built-in {path.Path.LastElement} in this context.");

                DMASTExpression? value = null;
                DMASTExpression? possibleValues = null;

                if (Check(TokenType.DM_DoubleSquareBracketEquals)) {
                    Whitespace();
                    value = Expression();
                } else if (Check(TokenType.DM_Equals)) {
                    Whitespace();
                    value = Expression();
                }

                var type = AsComplexTypes();
                Compiler.DMObjectTree.TryGetDMObject(path.Path, out var dmType);
                if (type is { Type: not DMValueType.Anything } && (value is null or DMASTConstantNull) && (dmType?.IsSubtypeOf(DreamPath.Datum) ?? false)) {
                    Compiler.Emit(WarningCode.ImplicitNullType, loc, $"Variable \"{path.Path}\" is null but not a subtype of atom nor explicitly typed as nullable, append \"|null\" to \"as\". It will implicitly be treated as nullable.");
                    type |= DMValueType.Null;
                }

                Whitespace();

                if (Check(TokenType.DM_In)) {
                    Whitespace();
                    possibleValues = Expression();
                }

                wasIndeterminate = false;

                return new DMASTDefinitionParameter(loc, path, value, type, possibleValues);
            }

            wasIndeterminate = Check(TokenType.DM_IndeterminateArgs);

            return null;
        }
}
