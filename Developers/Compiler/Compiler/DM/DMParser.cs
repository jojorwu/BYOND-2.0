using DMCompiler.Compiler.DMPreprocessor;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DMCompiler.Compiler.DM.AST;
using DMCompiler.DM;

namespace DMCompiler.Compiler.DM;

public partial class DMParser(DMCompiler compiler, DMLexer lexer) : Parser<Token>(compiler, lexer)
{
        protected Location CurrentLoc => Current().Location;
        protected DreamPath CurrentPath = DreamPath.Root;

        private bool _allowVarDeclExpression;

        private static readonly TokenType[] AssignTypes = [
            TokenType.DM_Equals,
            TokenType.DM_PlusEquals,
            TokenType.DM_MinusEquals,
            TokenType.DM_BarEquals,
            TokenType.DM_BarBarEquals,
            TokenType.DM_AndAndEquals,
            TokenType.DM_AndEquals,
            TokenType.DM_AndAndEquals,
            TokenType.DM_StarEquals,
            TokenType.DM_SlashEquals,
            TokenType.DM_LeftShiftEquals,
            TokenType.DM_RightShiftEquals,
            TokenType.DM_XorEquals,
            TokenType.DM_ModulusEquals,
            TokenType.DM_ModulusModulusEquals,
            TokenType.DM_AssignInto
        ];

        /// <remarks>This (and other similar TokenType[] sets here) is public because <see cref="DMPreprocessorParser"/> needs it.</remarks>
        public static readonly TokenType[] ComparisonTypes = [
            TokenType.DM_EqualsEquals,
            TokenType.DM_ExclamationEquals,
            TokenType.DM_TildeEquals,
            TokenType.DM_TildeExclamation
        ];

        public static readonly TokenType[] LtGtComparisonTypes = [
            TokenType.DM_LessThan,
            TokenType.DM_LessThanEquals,
            TokenType.DM_GreaterThan,
            TokenType.DM_GreaterThanEquals
        ];

        private static readonly TokenType[] ShiftTypes = [
            TokenType.DM_LeftShift,
            TokenType.DM_RightShift
        ];

        public static readonly TokenType[] PlusMinusTypes = [
            TokenType.DM_Plus,
            TokenType.DM_Minus
        ];

        public static readonly TokenType[] MulDivModTypes = [
            TokenType.DM_Star,
            TokenType.DM_Slash,
            TokenType.DM_Modulus,
            TokenType.DM_ModulusModulus
        ];

        private static readonly TokenType[] DereferenceTypes = [
            TokenType.DM_Period,
            TokenType.DM_Colon,
            TokenType.DM_DoubleColon, // not a dereference, but shares the same precedence
            TokenType.DM_QuestionPeriod,
            TokenType.DM_QuestionColon,
            TokenType.DM_QuestionLeftBracket
        ];

        private static readonly TokenType[] WhitespaceTypes = [
            TokenType.DM_Whitespace,
            TokenType.DM_Indent,
            TokenType.DM_Dedent
        ];

        private static readonly TokenType[] IdentifierTypes = [TokenType.DM_Identifier, TokenType.DM_Step, TokenType.DM_Proc];

        /// <summary>
        /// Used by <see cref="PathElement"/> to determine, keywords that may actually just be identifiers of a typename within a path, in a given context.
        /// </summary>
        private static readonly TokenType[] ValidPathElementTokens = [
            TokenType.DM_Identifier,
            TokenType.DM_Var,
            TokenType.DM_Proc,
            TokenType.DM_Step,
            TokenType.DM_Throw,
            TokenType.DM_Null,
            TokenType.DM_Switch,
            TokenType.DM_Spawn,
            TokenType.DM_Do,
            TokenType.DM_While,
            TokenType.DM_For
            //BYOND fails on DM_In, don't include that
        ];

        private static readonly TokenType[] ForSeparatorTypes = [
            TokenType.DM_Semicolon,
            TokenType.DM_Comma
        ];

        private static readonly TokenType[] OperatorOverloadTypes = [
            TokenType.DM_And,
            TokenType.DM_AndEquals,
            TokenType.DM_AssignInto,
            TokenType.DM_Bar,
            TokenType.DM_BarEquals,
            TokenType.DM_DoubleSquareBracket,
            TokenType.DM_DoubleSquareBracketEquals,
            TokenType.DM_GreaterThan,
            TokenType.DM_GreaterThanEquals,
            TokenType.DM_RightShift,
            TokenType.DM_RightShiftEquals,
            TokenType.DM_LeftShift,
            TokenType.DM_LeftShiftEquals,
            TokenType.DM_LessThan,
            TokenType.DM_LessThanEquals,
            TokenType.DM_Minus,
            TokenType.DM_MinusEquals,
            TokenType.DM_MinusMinus,
            TokenType.DM_Modulus,
            TokenType.DM_ModulusEquals,
            TokenType.DM_ModulusModulus,
            TokenType.DM_ModulusModulusEquals,
            TokenType.DM_Plus,
            TokenType.DM_PlusEquals,
            TokenType.DM_PlusPlus,
            TokenType.DM_Slash,
            TokenType.DM_SlashEquals,
            TokenType.DM_Star,
            TokenType.DM_StarEquals,
            TokenType.DM_StarStar,
            TokenType.DM_Tilde,
            TokenType.DM_TildeEquals,
            TokenType.DM_TildeExclamation,
            TokenType.DM_Xor,
            TokenType.DM_XorEquals,
            TokenType.DM_ConstantString
        ];

        // TEMPORARY - REMOVE WHEN IT MATCHES THE ABOVE
        private static readonly TokenType[] ImplementedOperatorOverloadTypes = [
            TokenType.DM_Plus,
            TokenType.DM_Minus,
            TokenType.DM_Star,
            TokenType.DM_StarEquals,
            TokenType.DM_Slash,
            TokenType.DM_SlashEquals,
            TokenType.DM_Bar,
            TokenType.DM_DoubleSquareBracket,
            TokenType.DM_DoubleSquareBracketEquals,
        ];

        public DMASTFile File() {
            var loc = Current().Location;
            List<DMASTStatement> statements = new();

            while (Current().Type != TokenType.EndOfFile) {
                List<DMASTStatement>? blockInner = BlockInner();
                if (blockInner != null)
                    statements.AddRange(blockInner);

                if (Current().Type != TokenType.EndOfFile) {
                    Token skipFrom = Current();
                    LocateNextTopLevel();
                    Warning($"Error recovery had to skip to {Current().Location}", token: skipFrom);
                }
            }

            Newline();
            Consume(TokenType.EndOfFile, "Expected EOF");
            return new DMASTFile(loc, new DMASTBlockInner(loc, statements.ToArray()));
        }

        private List<DMASTStatement>? BlockInner() {
            List<DMASTStatement> statements = new();

            do {
                Whitespace();
                DreamPath oldPath = CurrentPath;
                DMASTStatement? statement = Statement();

                CurrentPath = oldPath;

                if (statement != null) {
                    if (!PeekDelimiter() && Current().Type is not (TokenType.DM_Dedent or TokenType.DM_RightCurlyBracket or TokenType.EndOfFile)) {
                        Emit(WarningCode.BadToken, "Expected end of object statement");
                    }

                    Whitespace();
                    statements.Add(statement);
                } else {
                    if (statements.Count == 0) return null;
                }
            } while (Delimiter());
            Whitespace();

            return statements;
        }

        protected DMASTStatement? Statement() {
            var loc = CurrentLoc;

            if (Current().Type == TokenType.DM_Semicolon) { // A lone semicolon creates a "null statement" (like C)
                // Note that we do not consume the semicolon here
                return new DMASTNullStatement(loc);
            }

            DMASTPath? path = Path();
            if (path is null)
                return null;
            Whitespace();
            CurrentPath = CurrentPath.Combine(path.Path);

            //Object definition
            if (Block() is { } block) {
                Compiler.VerbosePrint($"Parsed object {CurrentPath}");
                return new DMASTObjectDefinition(loc, CurrentPath, block);
            }

            //Proc definition
            if (Check(TokenType.DM_LeftParenthesis)) {
                Compiler.VerbosePrint($"Parsing proc {CurrentPath}()");
                BracketWhitespace();
                var parameters = DefinitionParameters(out var wasIndeterminate);

                if (Current().Type != TokenType.DM_RightParenthesis && Current().Type != TokenType.DM_Comma && !wasIndeterminate) {
                    if (parameters.Count > 0) // Separate error handling mentions the missing right-paren
                        Emit(WarningCode.BadToken, $"{parameters.Last().Name}: missing comma ',' or right-paren ')'");

                    parameters.AddRange(DefinitionParameters(out wasIndeterminate));
                }

                if (!wasIndeterminate && Current().Type != TokenType.DM_RightParenthesis && Current().Type != TokenType.EndOfFile) {
                    // BYOND doesn't specify the arg
                    Emit(WarningCode.BadToken, $"Bad argument definition '{Current().PrintableText}'");
                    Advance();
                    BracketWhitespace();
                    Check(TokenType.DM_Comma);
                    BracketWhitespace();
                    parameters.AddRange(DefinitionParameters(out _));
                }

                BracketWhitespace();
                ConsumeRightParenthesis();
                Whitespace();

                // Proc return type
                var types = AsComplexTypes();

                DMASTProcBlockInner? procBlock = ProcBlock();
                if (procBlock is null) {
                    DMASTProcStatement? procStatement = ProcStatement();

                    if (procStatement is not null) {
                        procBlock = new DMASTProcBlockInner(loc, procStatement);
                    }
                }

                if (procBlock?.Statements.Length is 0 or null) {
                    Compiler.Emit(WarningCode.EmptyProc, loc,
                        "Empty proc detected - add an explicit \"return\" statement");
                }

                if (path.IsOperator && procBlock is not null) {
                    List<DMASTProcStatement> procStatements = procBlock.Statements.ToList();
                    Location tokenLoc = procBlock.Location;
                    //add ". = src" as the first expression in the operator
                    DMASTProcStatementExpression assignEqSrc = new DMASTProcStatementExpression(tokenLoc,
                        new DMASTAssign(tokenLoc, new DMASTCallableSelf(tokenLoc),
                            new DMASTIdentifier(tokenLoc, "src")));
                    procStatements.Insert(0, assignEqSrc);

                    procBlock = new DMASTProcBlockInner(loc, procStatements.ToArray(), procBlock.SetStatements);
                }

                return new DMASTProcDefinition(loc, CurrentPath, parameters.ToArray(), procBlock, types);
            }

            //Var definition(s)
            if (CurrentPath.FindElement("var") != -1) {
                bool isIndented = false;
                DreamPath varPath = CurrentPath;
                List<DMASTObjectVarDefinition> varDefinitions = new();

                var possibleNewline = Current();
                if (Newline()) {
                    if (Check(TokenType.DM_Indent)) {
                        isIndented = true;
                        DMASTPath? newVarPath = Path();
                        if (newVarPath == null) {
                            Emit(WarningCode.InvalidVarDefinition, "Expected a var definition");
                            return new DMASTInvalidStatement(CurrentLoc);
                        }

                        varPath = CurrentPath.AddToPath(newVarPath.Path.PathString);
                    } else {
                        ReuseToken(possibleNewline);
                    }
                } else if (Current().Type == TokenType.DM_Identifier) { // "var foo" instead of "var/foo"
                    DMASTPath? newVarPath = Path();
                    if (newVarPath == null) {
                        Emit(WarningCode.InvalidVarDefinition, "Expected a var definition");
                        return new DMASTInvalidStatement(CurrentLoc);
                    }

                    varPath = CurrentPath.AddToPath(newVarPath.Path.PathString);
                }

                while (true) {
                    Whitespace();

                    DMASTExpression? value = PathArray(ref varPath);

                    if (Check(TokenType.DM_Equals)) {
                        if (value != null) Warning("List doubly initialized");

                        Whitespace();
                        value = Expression();
                        RequireExpression(ref value);
                    } else if (Check(TokenType.DM_DoubleSquareBracketEquals)) {
                        if (value != null) Warning("List doubly initialized");

                        Whitespace();
                        value = Expression();
                        RequireExpression(ref value);
                    } else if (value == null) {
                        value = new DMASTConstantNull(loc);
                    }

                    var valType = AsComplexTypes() ?? DMValueType.Anything;
                    var varDef = new DMASTObjectVarDefinition(loc, varPath, value, valType);

                    if (varDef.IsStatic && varDef.Name is "usr" or "src" or "args" or "world" or "global" or "callee" or "caller")
                        Compiler.Emit(WarningCode.SoftReservedKeyword, loc, $"Global variable named {varDef.Name} DOES NOT override the built-in {varDef.Name}. This is a terrible idea, don't do that.");

                    varDefinitions.Add(varDef);
                    if (Check(TokenType.DM_Comma) || (isIndented && Delimiter())) {
                        Whitespace();
                        DMASTPath? newVarPath = Path();

                        if (newVarPath == null) {
                            Emit(WarningCode.InvalidVarDefinition, "Expected a var definition");
                            break;
                        }

                        varPath = CurrentPath.AddToPath(
                            isIndented ? newVarPath.Path.PathString
                                       : "../" + newVarPath.Path.PathString);
                    } else {
                        break;
                    }
                }

                if (isIndented)
                    Consume(TokenType.DM_Dedent, "Expected end of var block");

                return (varDefinitions.Count == 1)
                    ? varDefinitions[0]
                    : new DMASTMultipleObjectVarDefinitions(loc, varDefinitions.ToArray());
            }

            //Var override
            if (Check(TokenType.DM_Equals)) {
                Whitespace();
                DMASTExpression? value = Expression();
                RequireExpression(ref value);

                return new DMASTObjectVarOverride(loc, CurrentPath, value);
            }

            //Empty object definition
            Compiler.VerbosePrint($"Parsed object {CurrentPath} - empty");
            return new DMASTObjectDefinition(loc, CurrentPath, null);
        }

        private void RequirePath([NotNull] ref DMASTPath? path, string message = "Expected a path") {
            if (path == null) {
                Emit(WarningCode.BadExpression, message);
                path = new DMASTPath(Current().Location, DreamPath.Root);
            }
        }

        private bool Newline() {
            bool hasNewline = Check(TokenType.Newline);

            while (Check(TokenType.Newline)) {
            }
            return hasNewline;
        }

        private void Whitespace(bool includeIndentation = false) {
            if (includeIndentation) {
                while (Check(WhitespaceTypes)) { }
            } else {
                Check(TokenType.DM_Whitespace);
            }
        }

        //Inside brackets/parentheses, whitespace can include delimiters in select areas
        private void BracketWhitespace() {
            Whitespace();
            Delimiter();
            Whitespace();
        }

        private bool Delimiter() {
            bool hasDelimiter = false;
            while (Check(TokenType.DM_Semicolon) || Newline()) {
                hasDelimiter = true;
            }

            return hasDelimiter;
        }
}
