using System.Collections.Generic;
using System.Linq;
using DMCompiler.Compiler.DM.AST;
using DMCompiler.DM;

namespace DMCompiler.Compiler.DM;

public partial class DMParser
{
        private DMASTCallParameter[]? ProcCall() {
            if (Check(TokenType.DM_LeftParenthesis)) {
                BracketWhitespace();

                DMASTCallParameter[] callParameters = CallParameters() ?? [];
                BracketWhitespace();
                ConsumeRightParenthesis();

                return callParameters;
            }

            return null;
        }

        private DMASTPick.PickValue[]? PickArguments() {
            if (Check(TokenType.DM_LeftParenthesis)) {
                BracketWhitespace();

                DMASTPick.PickValue? arg = PickArgument();
                if (arg == null) {
                    Emit(WarningCode.MissingExpression, "Expected a pick argument");
                    arg = new(null, new DMASTInvalidExpression(CurrentLoc));
                }

                List<DMASTPick.PickValue> args = [arg.Value];

                while (Check(TokenType.DM_Comma)) {
                    BracketWhitespace();
                    arg = PickArgument();

                    if (arg != null) {
                        args.Add(arg.Value);
                    } else {
                        //A comma at the end is allowed, but the call must immediately be closed
                        if (Current().Type != TokenType.DM_RightParenthesis) {
                            Emit(WarningCode.MissingExpression, "Expected a pick argument");
                            break;
                        }
                    }
                }

                BracketWhitespace();
                ConsumeRightParenthesis();
                return args.ToArray();
            }

            return null;
        }

        private DMASTPick.PickValue? PickArgument() {
            DMASTExpression? expression = Expression();

            if (Check(TokenType.DM_Semicolon)) {
                Whitespace();
                DMASTExpression? value = Expression();
                RequireExpression(ref value);

                return new DMASTPick.PickValue(expression, value);
            } else if (expression != null) {
                return new DMASTPick.PickValue(null, expression);
            }

            return null;
        }

        private DMASTCallParameter[]? CallParameters() {
            List<DMASTCallParameter> parameters = new();
            DMASTCallParameter? parameter = CallParameter();
            BracketWhitespace();

            while (Check(TokenType.DM_Comma)) {
                BracketWhitespace();
                var loc = Current().Location;
                parameters.Add(parameter ?? new DMASTCallParameter(loc, new DMASTConstantNull(loc)));
                parameter = CallParameter();
                BracketWhitespace();
            }

            if (parameter != null) {
                parameters.Add(parameter);
            }

            if (parameters.Count > 0) {
                return parameters.ToArray();
            } else {
                return null;
            }
        }

        private DMASTCallParameter? CallParameter() {
            DMASTExpression? expression = Expression();
            if (expression == null)
                return null;

            if (expression is DMASTAssign assign) {
                DMASTExpression key = assign.LHS;
                if (key is DMASTIdentifier identifier) {
                    key = new DMASTConstantString(key.Location, identifier.Identifier);
                } else if (key is DMASTConstantNull) {
                    key = new DMASTConstantString(key.Location, "null");
                }

                return new DMASTCallParameter(assign.Location, assign.RHS, key);
            } else {
                return new DMASTCallParameter(expression.Location, expression);
            }
        }

        private DMASTExpression? Expression() {
            return ExpressionIn();
        }

        private void ExpressionTo(out DMASTExpression endRange, out DMASTExpression? step) {
            Whitespace();
            var endRangeExpr = ExpressionAssign();
            RequireExpression(ref endRangeExpr, "Missing end range");
            Whitespace();

            endRange = endRangeExpr;
            if (Check(TokenType.DM_Step)) {
                Whitespace();
                step = ExpressionAssign();
                RequireExpression(ref step, "Missing step value");
                Whitespace();
            } else {
                step = null;
            }
        }

        private DMASTExpression? ExpressionIn() {
            var loc = Current().Location; // Don't check this inside, as Check() will advance and point at next token instead
            DMASTExpression? value = ExpressionAssign();

            while (value != null && Check(TokenType.DM_In)) {

                Whitespace();
                DMASTExpression? list = ExpressionAssign();
                RequireExpression(ref list, "Expected a container to search in");
                Whitespace();

                if (Check(TokenType.DM_To)) {
                    ExpressionTo(out var endRange, out var step);
                    return new DMASTExpressionInRange(loc, value, list, endRange, step);
                }

                value = new DMASTExpressionIn(loc, value, list);
            }

            return value;
        }

        private DMASTExpression? ExpressionAssign() {
            DMASTExpression? expression = ExpressionTernary();

            if (expression != null) {
                Token token = Current();
                if (Check(AssignTypes)) {
                    Whitespace();
                    DMASTExpression? value = ExpressionAssign();
                    RequireExpression(ref value, "Expected a value");

                    switch (token.Type) {
                        case TokenType.DM_Equals: return new DMASTAssign(token.Location, expression, value);
                        case TokenType.DM_PlusEquals: return new DMASTAppend(token.Location, expression, value);
                        case TokenType.DM_MinusEquals: return new DMASTRemove(token.Location, expression, value);
                        case TokenType.DM_BarEquals: return new DMASTCombine(token.Location, expression, value);
                        case TokenType.DM_BarBarEquals: return new DMASTLogicalOrAssign(token.Location, expression, value);
                        case TokenType.DM_AndEquals: return new DMASTMask(token.Location, expression, value);
                        case TokenType.DM_AndAndEquals: return new DMASTLogicalAndAssign(token.Location, expression, value);
                        case TokenType.DM_StarEquals: return new DMASTMultiplyAssign(token.Location, expression, value);
                        case TokenType.DM_SlashEquals: return new DMASTDivideAssign(token.Location, expression, value);
                        case TokenType.DM_LeftShiftEquals: return new DMASTLeftShiftAssign(token.Location, expression, value);
                        case TokenType.DM_RightShiftEquals: return new DMASTRightShiftAssign(token.Location, expression, value);
                        case TokenType.DM_XorEquals: return new DMASTXorAssign(token.Location, expression, value);
                        case TokenType.DM_ModulusEquals: return new DMASTModulusAssign(token.Location, expression, value);
                        case TokenType.DM_ModulusModulusEquals: return new DMASTModulusModulusAssign(token.Location, expression, value);
                        case TokenType.DM_AssignInto: return new DMASTAssignInto(token.Location, expression, value);
                    }
                }
            }

            return expression;
        }

        private DMASTExpression? ExpressionTernary(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionOr(isTernaryB);

            if (a != null && Check(TokenType.DM_Question)) {
                Whitespace();
                DMASTExpression? b = ExpressionTernary(isTernaryB: true);
                RequireExpression(ref b);

                if (b is DMASTVoid) b = new DMASTConstantNull(b.Location);

                Consume(TokenType.DM_Colon, "Expected ':'");
                Whitespace();

                DMASTExpression? c = ExpressionTernary(isTernaryB);
                if (c is DMASTVoid) c = new DMASTConstantNull(c.Location);

                return new DMASTTernary(a.Location, a, b, c ?? new DMASTConstantNull(a.Location));
            }

            return a;
        }

        private DMASTExpression? ExpressionOr(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionAnd(isTernaryB);
            if (a != null) {
                var loc = Current().Location;

                while (Check(TokenType.DM_BarBar)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionAnd(isTernaryB);
                    RequireExpression(ref b, "Expected a second value");

                    a = new DMASTOr(loc, a, b);
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionAnd(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionBinaryOr(isTernaryB);

            if (a != null) {
                var loc = Current().Location;

                while (Check(TokenType.DM_AndAnd)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionBinaryOr(isTernaryB);
                    RequireExpression(ref b, "Expected a second value");

                    a = new DMASTAnd(loc, a, b);
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionBinaryOr(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionBinaryXor(isTernaryB);
            if (a != null) {
                var loc = Current().Location;

                while (Check(TokenType.DM_Bar)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionBinaryXor(isTernaryB);
                    RequireExpression(ref b);

                    a = new DMASTBinaryOr(loc, a, b);
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionBinaryXor(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionBinaryAnd(isTernaryB);
            if (a != null) {
                var loc = Current().Location;

                while (Check(TokenType.DM_Xor)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionBinaryAnd(isTernaryB);
                    RequireExpression(ref b);

                    a = new DMASTBinaryXor(loc, a, b);
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionBinaryAnd(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionComparison(isTernaryB);
            if (a != null) {
                var loc = Current().Location;

                while (Check(TokenType.DM_And)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionComparison(isTernaryB);
                    RequireExpression(ref b);

                    a = new DMASTBinaryAnd(loc, a, b);
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionComparison(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionBitShift(isTernaryB);

            if (a != null) {
                Token token = Current();

                while (Check(ComparisonTypes)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionBitShift(isTernaryB);
                    RequireExpression(ref b, "Expected an expression to compare to");

                    switch (token.Type) {
                        case TokenType.DM_EqualsEquals: a = new DMASTEqual(token.Location, a, b); break;
                        case TokenType.DM_ExclamationEquals: a = new DMASTNotEqual(token.Location, a, b); break;
                        case TokenType.DM_TildeEquals: a = new DMASTEquivalent(token.Location, a, b); break;
                        case TokenType.DM_TildeExclamation: a = new DMASTNotEquivalent(token.Location, a, b); break;
                    }

                    token = Current();
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionBitShift(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionComparisonLtGt(isTernaryB);

            if (a != null) {
                Token token = Current();

                while (Check(ShiftTypes)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionComparisonLtGt(isTernaryB);
                    RequireExpression(ref b);

                    switch (token.Type) {
                        case TokenType.DM_LeftShift: a = new DMASTLeftShift(token.Location, a, b); break;
                        case TokenType.DM_RightShift: a = new DMASTRightShift(token.Location, a, b); break;
                    }

                    token = Current();
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionComparisonLtGt(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionAdditionSubtraction(isTernaryB);

            if (a != null) {
                Token token = Current();

                while (Check(LtGtComparisonTypes)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionAdditionSubtraction(isTernaryB);
                    RequireExpression(ref b);

                    switch (token.Type) {
                        case TokenType.DM_LessThan: a = new DMASTLessThan(token.Location, a, b); break;
                        case TokenType.DM_LessThanEquals: a = new DMASTLessThanOrEqual(token.Location, a, b); break;
                        case TokenType.DM_GreaterThan: a = new DMASTGreaterThan(token.Location, a, b); break;
                        case TokenType.DM_GreaterThanEquals: a = new DMASTGreaterThanOrEqual(token.Location, a, b); break;
                    }

                    token = Current();
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionAdditionSubtraction(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionMultiplicationDivisionModulus(isTernaryB);

            if (a != null) {
                Token token = Current();

                while (Check(PlusMinusTypes)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionMultiplicationDivisionModulus(isTernaryB);
                    RequireExpression(ref b);

                    switch (token.Type) {
                        case TokenType.DM_Plus: a = new DMASTAdd(token.Location, a, b); break;
                        case TokenType.DM_Minus: a = new DMASTSubtract(token.Location, a, b); break;
                    }

                    token = Current();
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionMultiplicationDivisionModulus(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionPower(isTernaryB);

            if (a != null) {
                Token token = Current();

                while (Check(MulDivModTypes)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionPower(isTernaryB);
                    RequireExpression(ref b);

                    switch (token.Type) {
                        case TokenType.DM_Star: a = new DMASTMultiply(token.Location, a, b); break;
                        case TokenType.DM_Slash: a = new DMASTDivide(token.Location, a, b); break;
                        case TokenType.DM_Modulus: a = new DMASTModulus(token.Location, a, b); break;
                        case TokenType.DM_ModulusModulus: a = new DMASTModulusModulus(token.Location, a, b); break;
                    }

                    token = Current();
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionPower(bool isTernaryB = false) {
            DMASTExpression? a = ExpressionUnary(isTernaryB);

            if (a != null) {
                var loc = Current().Location;

                while (Check(TokenType.DM_StarStar)) {
                    Whitespace();
                    DMASTExpression? b = ExpressionPower(isTernaryB);
                    RequireExpression(ref b);

                    a = new DMASTPower(loc, a, b);
                }
            }

            return a;
        }

        private DMASTExpression? ExpressionUnary(bool isTernaryB = false) {
            var loc = CurrentLoc;

            if (Check(stackalloc[] {
                    TokenType.DM_Exclamation,
                    TokenType.DM_Tilde,
                    TokenType.DM_PlusPlus,
                    TokenType.DM_MinusMinus,
                    TokenType.DM_And,
                    TokenType.DM_Star
                }, out var unaryToken)) {
                Whitespace();
                DMASTExpression? expression = ExpressionUnary(isTernaryB);
                RequireExpression(ref expression);

                switch (unaryToken.Type) {
                    case TokenType.DM_Exclamation: return new DMASTNot(loc, expression);
                    case TokenType.DM_Tilde: return new DMASTBinaryNot(loc, expression);
                    case TokenType.DM_PlusPlus: return new DMASTPreIncrement(loc, expression);
                    case TokenType.DM_MinusMinus: return new DMASTPreDecrement(loc, expression);
                    case TokenType.DM_And: return new DMASTPointerRef(loc, expression);
                    case TokenType.DM_Star: return new DMASTPointerDeref(loc, expression);
                }

                Emit(WarningCode.BadToken, loc, $"Problem while handling unary '{unaryToken.PrintableText}'");
                return new DMASTInvalidExpression(loc);
            } else {
                DMASTExpression? expression = ExpressionSign(isTernaryB);

                if (expression != null) {
                    if (Check(TokenType.DM_PlusPlus)) {
                        Whitespace();
                        expression = new DMASTPostIncrement(loc, expression);
                    } else if (Check(TokenType.DM_MinusMinus)) {
                        Whitespace();
                        expression = new DMASTPostDecrement(loc, expression);
                    }
                }

                return expression;
            }
        }

        private DMASTExpression? ExpressionSign(bool isTernaryB = false) {
            Token token = Current();

            if (Check(PlusMinusTypes)) {
                Whitespace();
                DMASTExpression? expression = ExpressionSign();
                RequireExpression(ref expression);

                if (token.Type == TokenType.DM_Minus) {
                    switch (expression) {
                        case DMASTConstantInteger integer:
                            return new DMASTConstantInteger(token.Location, -integer.Value);
                        case DMASTConstantFloat constantFloat:
                            return new DMASTConstantFloat(token.Location, -constantFloat.Value);
                        default:
                            return new DMASTNegate(token.Location, expression);
                    }
                } else {
                    return expression;
                }
            }

            return ExpressionNew(isTernaryB);
        }

        private DMASTExpression? ExpressionNew(bool isTernaryB = false) {
            var loc = Current().Location;

            if (Check(TokenType.DM_New)) {
                Whitespace();
                DMASTExpression? type = ExpressionPrimary(allowParentheses: false);
                type = ParseDereference(type, allowCalls: false);
                DMASTCallParameter[]? parameters = ProcCall();

                DMASTExpression? newExpression = type switch {
                    DMASTConstantPath path => new DMASTNewPath(loc, path, parameters),
                    not null => new DMASTNewExpr(loc, type, parameters),
                    null => new DMASTNewInferred(loc, parameters),
                };

                newExpression = ParseDereference(newExpression);
                return newExpression;
            }

            return ParseDereference(ExpressionPrimary(), true, isTernaryB);
        }

        private DMASTExpression? ExpressionPrimary(bool allowParentheses = true) {
            var token = Current();
            var loc = token.Location;

            if (allowParentheses && Check(TokenType.DM_LeftParenthesis)) {
                BracketWhitespace();
                DMASTExpression? inner = Expression();
                BracketWhitespace();
                ConsumeRightParenthesis();

                if (inner is null) {
                    inner = new DMASTVoid(loc);
                } else {
                    inner = new DMASTExpressionWrapped(loc, inner);
                }

                return inner;
            }

            if (token.Type == TokenType.DM_Var && _allowVarDeclExpression) {
                var varPath = Path();
                RequirePath(ref varPath);
                return new DMASTVarDeclExpression(loc, varPath);
            }

            if (Constant() is { } constant)
                return constant;

            if (Path(true) is { } path) {
                DMASTExpressionConstant pathConstant = new DMASTConstantPath(loc, path);

                while (Check(TokenType.DM_Period)) {
                    DMASTPath? search = Path();
                    if (search != null) {
                        pathConstant = new DMASTUpwardPathSearch(loc, pathConstant, search);
                    }
                }

                Whitespace(); // whitespace between path and modified type

                //TODO actual modified type support
                if (Check(TokenType.DM_LeftCurlyBracket)) {
                    Compiler.UnimplementedWarning(path.Location, "Modified types are currently not supported and modified values will be ignored.");

                    BracketWhitespace();
                    Check(TokenType.DM_Indent); // The body could be indented. We ignore that. TODO: Better braced block parsing
                    DMASTIdentifier? overriding = Identifier();

                    while (overriding != null) {
                        BracketWhitespace();
                        Consume(TokenType.DM_Equals, "Expected '='");
                        BracketWhitespace();

                        Expression(); // TODO: Use this (one day...)

                        if (Check(TokenType.DM_Semicolon)) {
                            BracketWhitespace();
                            overriding = Identifier();
                        } else {
                            overriding = null;
                        }
                    }

                    Check(TokenType.DM_Dedent); // We ignore indents/dedents in the body
                    BracketWhitespace();
                    Consume(TokenType.DM_RightCurlyBracket, "Expected '}'");
                    //The lexer tosses in a newline after '}', but we avoid Newline() because we only want to remove the extra newline, not all of them
                    Check(TokenType.Newline);
                }

                return pathConstant;
            }

            if (Identifier() is { } identifier)
                return identifier;

            if ((DMASTExpression?)Callable() is { } callable)
                return callable;

            if (Check(TokenType.DM_DoubleColon))
                return ParseScopeIdentifier(null);

            if (Check(TokenType.DM_Call)) {
                Whitespace();
                DMASTCallParameter[]? callParameters = ProcCall();

                bool invalid = false;
                if (callParameters == null || callParameters.Length < 1 || callParameters.Length > 2) {
                    Emit(WarningCode.InvalidArgumentCount, "call()() must have 2 parameters");
                    invalid = true; // we want to parse the second pair of parentheses still
                }

                Whitespace();
                DMASTCallParameter[]? procParameters = ProcCall();
                if (procParameters == null) {
                    Emit(WarningCode.InvalidArgumentCount, "Expected proc parameters");
                    procParameters = [];
                }

                return invalid ? new DMASTInvalidExpression(loc): new DMASTCall(loc, callParameters!, procParameters);
            }

            return null;
        }

        protected DMASTExpression? Constant() {
            Token constantToken = Current();

            switch (constantToken.Type) {
                case TokenType.DM_Integer: Advance(); return new DMASTConstantInteger(constantToken.Location, constantToken.ValueAsInt());
                case TokenType.DM_Float: Advance(); return new DMASTConstantFloat(constantToken.Location, constantToken.ValueAsFloat());
                case TokenType.DM_Resource: Advance(); return new DMASTConstantResource(constantToken.Location, constantToken.ValueAsString() ?? "");
                case TokenType.DM_Null: Advance(); return new DMASTConstantNull(constantToken.Location);
                case TokenType.DM_RawString: Advance(); return new DMASTConstantString(constantToken.Location, constantToken.ValueAsString() ?? "");
                case TokenType.DM_ConstantString:
                case TokenType.DM_StringBegin:
                    // Don't advance, ExpressionFromString() will handle it
                    return ExpressionFromString();
                default: return null;
            }
        }

        private DMASTExpression? ParseDereference(DMASTExpression? expression, bool allowCalls = true, bool isTernaryB = false) {
            // We don't compile expression-calls as dereferences, but they have very similar precedence
            if (allowCalls) {
                expression = ParseProcCall(expression);
            }

            if (expression != null) {
                List<DMASTDereference.Operation> operations = new();
                bool ternaryBHasPriority = expression is not DMASTIdentifier;

                while (true) {
                    Token token = Current();

                    // Check for a valid deref operation token
                    if (!Check(DereferenceTypes)) {
                        Whitespace();

                        token = Current();

                        if (!Check(TokenType.DM_LeftBracket)) {
                            break;
                        }
                    }

                    // Cancel this operation chain (and potentially fall back to ternary behaviour) if this looks more like part of a ternary expression than a deref
                    if (token.Type == TokenType.DM_Colon) {
                        bool invalidDereference = (expression is DMASTExpressionConstant);

                        if (!invalidDereference) {
                            Token innerToken = Current();

                            if (Check(IdentifierTypes)) {
                                ReuseToken(innerToken);
                            } else {
                                invalidDereference = true;
                            }
                        }

                        if (invalidDereference) {
                            ReuseToken(token);
                            break;
                        }
                    }

                    // `:` token should preemptively end our dereference when inside the `b` operand of a ternary
                    // but not for the first dereference if the base expression is an identifier!
                    if (isTernaryB && ternaryBHasPriority && token.Type == TokenType.DM_Colon) {
                        ReuseToken(token);
                        break;
                    }

                    DMASTDereference.Operation operation;

                    switch (token.Type) {
                        case TokenType.DM_Colon:
                            Compiler.Emit(WarningCode.RuntimeSearchOperator, token.Location, "Runtime search operator ':' should be avoided; prefer typecasting and using '.' instead");
                            goto case TokenType.DM_QuestionPeriod;
                        case TokenType.DM_QuestionColon:
                            Compiler.Emit(WarningCode.RuntimeSearchOperator, token.Location, "Runtime search operator '?:' should be avoided; prefer typecasting and using '?.' instead");
                            goto case TokenType.DM_QuestionPeriod;
                        case TokenType.DM_Period:
                        case TokenType.DM_QuestionPeriod:
                         {
                            var identifier = Identifier();

                            if (identifier == null) {
                                Compiler.Emit(WarningCode.BadToken, token.Location, "Identifier expected");
                                return new DMASTConstantNull(token.Location);
                            }

                            operation = new DMASTDereference.FieldOperation {
                                Location = identifier.Location,
                                Safe = token.Type is TokenType.DM_QuestionPeriod or TokenType.DM_QuestionColon,
                                Identifier = identifier.Identifier,
                                NoSearch = token.Type is TokenType.DM_Colon or TokenType.DM_QuestionColon
                            };
                            break;
                        }

                        case TokenType.DM_DoubleColon: {
                            if (operations.Count != 0) {
                                expression = new DMASTDereference(expression.Location, expression, operations.ToArray());
                                operations.Clear();
                            }

                            expression = ParseScopeIdentifier(expression);
                            if (expression is null)
                                return null;
                            continue;
                        }

                        case TokenType.DM_LeftBracket:
                        case TokenType.DM_QuestionLeftBracket: {
                            ternaryBHasPriority = true;

                            Whitespace();
                            var index = Expression();
                            ConsumeRightBracket();

                            if (index == null) {
                                Compiler.Emit(WarningCode.BadToken, token.Location, "Expression expected");
                                return new DMASTConstantNull(token.Location);
                            }

                            operation = new DMASTDereference.IndexOperation {
                                Index = index,
                                Location = index.Location,
                                Safe = token.Type is TokenType.DM_QuestionLeftBracket
                            };
                            break;
                        }

                        default:
                            throw new System.InvalidOperationException("unhandled dereference token");
                    }

                    // Attempt to upgrade this operation to a call
                    if (allowCalls) {
                        Whitespace();

                        var parameters = ProcCall();

                        if (parameters != null) {
                            ternaryBHasPriority = true;

                            switch (operation) {
                                case DMASTDereference.FieldOperation fieldOperation:
                                    operation = new DMASTDereference.CallOperation {
                                        Parameters = parameters,
                                        Location = fieldOperation.Location,
                                        Safe = fieldOperation.Safe,
                                        Identifier = fieldOperation.Identifier,
                                        NoSearch = fieldOperation.NoSearch
                                    };
                                    break;

                                case DMASTDereference.IndexOperation:
                                    Compiler.Emit(WarningCode.BadToken, token.Location, "Attempt to call an invalid l-value");
                                    return new DMASTConstantNull(token.Location);

                                default:
                                    throw new System.InvalidOperationException("unhandled dereference operation kind");
                            }
                        }
                    }

                    operations.Add(operation);
                }

                if (operations.Count != 0) {
                    Whitespace();
                    return new DMASTDereference(expression.Location, expression, operations.ToArray());
                }
            }

            Whitespace();
            return expression;
        }

        private DMASTExpression? ParseProcCall(DMASTExpression? expression) {
            if (expression is IDMASTCallable callable) {
                Whitespace();

                return ProcCall() is { } parameters
                    ? new DMASTProcCall(expression.Location, callable, parameters)
                    : expression; // Not a proc call
            }

            if (expression is not DMASTIdentifier identifier)
                return expression;

            Whitespace();

            if (identifier.Identifier == "pick") {
                DMASTPick.PickValue[]? pickValues = PickArguments();

                if (pickValues != null) {
                    return new DMASTPick(identifier.Location, pickValues);
                }
            }

            DMASTCallParameter[]? callParameters = ProcCall();
            if (callParameters != null) {
                var procName = identifier.Identifier;
                var callLoc = identifier.Location;

                switch (procName) {
                    // Any number of arguments
                    case "list": return new DMASTList(callLoc, callParameters, false);
                    case "alist": return new DMASTList(callLoc, callParameters, true);
                    case "newlist": return new DMASTNewList(callLoc, callParameters);
                    case "addtext": return new DMASTAddText(callLoc, callParameters);
                    case "gradient": return new DMASTGradient(callLoc, callParameters);

                    // 1 argument
                    case "prob":
                    case "initial":
                    case "nameof":
                    case "issaved":
                    case "sin":
                    case "cos":
                    case "arcsin":
                    case "tan":
                    case "arccos":
                    case "abs":
                    case "sqrt":
                    case "isnull":
                    case "length": {
                        if (callParameters.Length != 1) {
                            Emit(WarningCode.InvalidArgumentCount, callLoc, $"{procName}() takes 1 argument");
                            return new DMASTInvalidExpression(callLoc);
                        }

                        var arg = callParameters[0];

                        if (arg.Key != null)
                            Emit(WarningCode.InvalidArgumentKey, arg.Key.Location,
                                $"{procName}() does not take a named argument");

                        switch (procName) {
                            case "prob": return new DMASTProb(callLoc, arg.Value);
                            case "initial": return new DMASTInitial(callLoc, arg.Value);
                            case "nameof": return new DMASTNameof(callLoc, arg.Value);
                            case "issaved": return new DMASTIsSaved(callLoc, arg.Value);
                            case "sin": return new DMASTSin(callLoc, arg.Value);
                            case "cos": return new DMASTCos(callLoc, arg.Value);
                            case "arcsin": return new DMASTArcsin(callLoc, arg.Value);
                            case "tan": return new DMASTTan(callLoc, arg.Value);
                            case "arccos": return new DMASTArccos(callLoc, arg.Value);
                            case "abs": return new DMASTAbs(callLoc, arg.Value);
                            case "sqrt": return new DMASTSqrt(callLoc, arg.Value);
                            case "isnull": return new DMASTIsNull(callLoc, arg.Value);
                            case "length": return new DMASTLength(callLoc, arg.Value);
                        }

                        Emit(WarningCode.BadExpression, callLoc, $"Problem while handling {procName}");
                        return new DMASTInvalidExpression(callLoc);
                    }

                    // 2 arguments
                    case "get_step":
                    case "get_dir": {
                        if (callParameters.Length != 2) {
                            Emit(WarningCode.InvalidArgumentCount, callLoc, $"{procName}() takes 2 arguments");
                            return new DMASTInvalidExpression(callLoc);
                        }

                        return (procName == "get_step")
                            ? new DMASTGetStep(callLoc, callParameters[0].Value, callParameters[1].Value)
                            : new DMASTGetDir(callLoc, callParameters[0].Value, callParameters[1].Value);
                    }

                    case "input": {
                        Whitespace();
                        DMValueType? types = AsTypes();
                        Whitespace();
                        DMASTExpression? list = null;

                        if (Check(TokenType.DM_In)) {
                            Whitespace();
                            list = Expression();
                        }

                        return new DMASTInput(callLoc, callParameters, types, list);
                    }
                    case "arctan": {
                        if (callParameters.Length != 1 && callParameters.Length != 2) {
                            Emit(WarningCode.InvalidArgumentCount, callLoc, "arctan() requires 1 or 2 arguments");
                            return new DMASTInvalidExpression(callLoc);
                        }

                        return callParameters.Length == 1
                            ? new DMASTArctan(callLoc, callParameters[0].Value)
                            : new DMASTArctan2(callLoc, callParameters[0].Value, callParameters[1].Value);
                    }
                    case "log": {
                        if (callParameters.Length != 1 && callParameters.Length != 2) {
                            Emit(WarningCode.InvalidArgumentCount, callLoc, "log() requires 1 or 2 arguments");
                            return new DMASTInvalidExpression(callLoc);
                        }

                        return callParameters.Length == 1
                            ? new DMASTLog(callLoc, callParameters[0].Value, null)
                            : new DMASTLog(callLoc, callParameters[1].Value, callParameters[0].Value);
                    }
                    case "astype": {
                        if (callParameters.Length != 1 && callParameters.Length != 2) {
                            Emit(WarningCode.InvalidArgumentCount, callLoc, "astype() requires 1 or 2 arguments");
                            return new DMASTInvalidExpression(callLoc);
                        }

                        return callParameters.Length == 1
                            ? new DMASTImplicitAsType(callLoc, callParameters[0].Value)
                            : new DMASTAsType(callLoc, callParameters[0].Value, callParameters[1].Value);
                    }
                    case "istype": {
                        if (callParameters.Length != 1 && callParameters.Length != 2) {
                            Emit(WarningCode.InvalidArgumentCount, callLoc, "istype() requires 1 or 2 arguments");
                            return new DMASTInvalidExpression(callLoc);
                        }

                        return callParameters.Length == 1
                            ? new DMASTImplicitIsType(callLoc, callParameters[0].Value)
                            : new DMASTIsType(callLoc, callParameters[0].Value, callParameters[1].Value);
                    }
                    case "text": {
                        if (callParameters.Length == 0) {
                            Emit(WarningCode.InvalidArgumentCount, callLoc, "text() requires at least 1 argument");
                            return new DMASTInvalidExpression(callLoc);
                        }

                        switch (callParameters[0].Value) {
                            case DMASTConstantString constantString: {
                                if (callParameters.Length > 1)
                                    Emit(WarningCode.InvalidArgumentCount, callLoc, "text() expected 1 argument");

                                return constantString;
                            }
                            case DMASTStringFormat formatText: {
                                List<int> emptyValueIndices = new();
                                for (int i = 0; i < formatText.InterpolatedValues.Length; i++) {
                                    if (formatText.InterpolatedValues[i] == null) emptyValueIndices.Add(i);
                                }

                                if (callParameters.Length != emptyValueIndices.Count + 1) {
                                    Emit(WarningCode.InvalidArgumentCount, callLoc,
                                        "text() was given an invalid amount of arguments for the string");
                                    return new DMASTInvalidExpression(callLoc);
                                }

                                for (int i = 0; i < emptyValueIndices.Count; i++) {
                                    int emptyValueIndex = emptyValueIndices[i];

                                    formatText.InterpolatedValues[emptyValueIndex] = callParameters[i + 1].Value;
                                }

                                return formatText;
                            }
                            default:
                                Emit(WarningCode.BadArgument, callParameters[0].Location,
                                    "text() expected a string as the first argument");
                                return new DMASTInvalidExpression(callLoc);
                        }
                    }
                    case "locate": {
                        if (callParameters.Length > 3) {
                            Emit(WarningCode.InvalidArgumentCount, callLoc,
                                "locate() was given too many arguments");
                            return new DMASTInvalidExpression(callLoc);
                        }

                        if (callParameters.Length == 3) { //locate(X, Y, Z)
                            return new DMASTLocateCoordinates(callLoc, callParameters[0].Value, callParameters[1].Value, callParameters[2].Value);
                        } else {
                            Whitespace();

                            DMASTExpression? container = null;
                            if (Check(TokenType.DM_In)) {
                                Whitespace();

                                container = Expression();
                                RequireExpression(ref container, "Expected a container for locate()");
                            }

                            DMASTExpression? type = null;
                            if (callParameters.Length == 2) {
                                type = callParameters[0].Value;
                                container = callParameters[1].Value;
                            } else if (callParameters.Length == 1) {
                                type = callParameters[0].Value;
                            }

                            return new DMASTLocate(callLoc, type, container);
                        }
                    }
                    case "rgb": {
                        if (callParameters.Length is < 3 or > 5)
                            Emit(WarningCode.InvalidArgumentCount, callLoc,
                                "Expected 3 to 5 arguments for rgb()");

                        return new DMASTRgb(identifier.Location, callParameters);
                    }
                    default:
                        return new DMASTProcCall(callLoc, new DMASTCallableProcIdentifier(callLoc, identifier.Identifier), callParameters);
                }
            }

            return expression;
        }
}
