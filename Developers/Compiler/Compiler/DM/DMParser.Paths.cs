using System.Collections.Generic;
using System.Linq;
using DMCompiler.Compiler.DM.AST;
using DMCompiler.DM;

namespace DMCompiler.Compiler.DM;

public partial class DMParser
{
        /// <summary>
        /// Tries to read in a path. Returns null if one cannot be constructed.
        /// </summary>
        protected DMASTPath? Path(bool expression = false) {
            Token firstToken = Current();
            DreamPath.PathType pathType = DreamPath.PathType.Relative;
            bool hasPathTypeToken = true;

            if (Check(TokenType.DM_Slash)) {
                // Check if they did "/.whatever/" instead of ".whatever/"
                pathType = Check(TokenType.DM_Period) ? DreamPath.PathType.UpwardSearch : DreamPath.PathType.Absolute;
            } else if (Check(TokenType.DM_Colon)) {
                pathType = DreamPath.PathType.DownwardSearch;
            } else if (Check(TokenType.DM_Period)) {
                pathType = DreamPath.PathType.UpwardSearch;
            } else {
                hasPathTypeToken = false;

                if (expression) return null;
            }

            string? pathElement = PathElement();
            if (pathElement != null) {
                List<string> pathElements = [pathElement];
                bool operatorFlag = false;
                while (pathElement != null && Check(TokenType.DM_Slash)) {
                    pathElement = PathElement();

                    if (pathElement != null) {
                        if(pathElement == "operator") {
                            Token operatorToken = Current();
                            if(Current().Type == TokenType.DM_Slash) {
                                //Up to this point, it's ambiguous whether it's a slash to mean operator/(), like the division operator overload
                                //or "operator" just being used as a normal type name, as in a/operator/b/c/d
                                Token peekToken = Advance();
                                if (peekToken.Type == TokenType.DM_LeftParenthesis) { // Disambiguated as an overload
                                    operatorFlag = true;
                                    pathElement += operatorToken.PrintableText;
                                } else { //Otherwise it's just a normal path, resume
                                    ReuseToken(operatorToken);
                                    Emit(WarningCode.SoftReservedKeyword, "Using \"operator\" as a path element is ambiguous");
                                }
                            } else if (Check(OperatorOverloadTypes)) {
                                if (operatorToken is { Type: TokenType.DM_ConstantString, Value: not "" }) {
                                    Compiler.Emit(WarningCode.BadToken, operatorToken.Location,
                                        "The quotes in a stringify overload must be empty");
                                }

                                if (!ImplementedOperatorOverloadTypes.Contains(operatorToken.Type)) {
                                    Compiler.UnimplementedWarning(operatorToken.Location,
                                        $"operator{operatorToken.PrintableText} overloads are not implemented. They will be defined but never called.");
                                }

                                operatorFlag = true;
                                pathElement += operatorToken.PrintableText;
                            }
                        }

                        pathElements.Add(pathElement);
                    }
                }

                return new DMASTPath(firstToken.Location, new DreamPath(pathType, pathElements.ToArray()), operatorFlag);
            } else if (hasPathTypeToken) {
                if (expression) ReuseToken(firstToken);

                return null;
            }

            return null;
        }

        /// <summary>
        /// Extracts the text from this token if it is reasonable for it to appear as a typename in a path.
        /// </summary>
        /// <returns>The <see cref="Token.Text"/> if this is a valid path element, null otherwise.</returns>
        private string? PathElement() {
            Token elementToken = Current();
            if (Check(ValidPathElementTokens)) {
                return elementToken.Text ?? null;
            } else {
                return null;
            }
        }

        private DMASTDimensionalList? PathArray(ref DreamPath path) {
            if (Current().Type == TokenType.DM_LeftBracket || Current().Type == TokenType.DM_DoubleSquareBracket) {
                var loc = Current().Location;

                // Trying to use path.IsDescendantOf(DreamPath.List) here doesn't work
                if (!path.Elements[..^1].Contains("list")) {
                    var elements = path.Elements.ToList();
                    elements.Insert(elements.IndexOf("var") + 1, "list");
                    path = new DreamPath("/" + string.Join("/", elements));
                }

                List<DMASTExpression> sizes = new(2); // Most common is 1D or 2D lists

                while (true) {
                    if(Check(TokenType.DM_DoubleSquareBracket))
                        Whitespace();
                    else if(Check(TokenType.DM_LeftBracket)) {
                        Whitespace();
                        var size = Expression();
                        if (size is not null) {
                            sizes.Add(size);
                        }

                        ConsumeRightBracket();
                        Whitespace();
                    } else
                        break;
                }

                if (sizes.Count > 0) {
                    return new DMASTDimensionalList(loc, sizes);
                }
            }

            return null;
        }

        private IDMASTCallable? Callable() {
            var loc = Current().Location;
            if (Check(TokenType.DM_SuperProc)) return new DMASTCallableSuper(loc);
            if (Check(TokenType.DM_Period)) return new DMASTCallableSelf(loc);

            return null;
        }

        private DMASTExpression? ParseScopeIdentifier(DMASTExpression? expression) {
            do {
                var identifier = Identifier();
                if (identifier == null) {
                    Compiler.Emit(WarningCode.BadToken, Current().Location, "Identifier expected");
                    return null;
                }

                var location = expression?.Location ?? identifier.Location; // TODO: Should be on the :: token if expression is null
                var parameters = ProcCall();
                expression = new DMASTScopeIdentifier(location, expression, identifier.Identifier, parameters);
            } while (Check(TokenType.DM_DoubleColon));

            return expression;
        }

        private DMASTIdentifier? Identifier() {
            Token token = Current();
            return Check(IdentifierTypes) ? new DMASTIdentifier(token.Location, token.Text) : null;
        }

        private DMValueType? AsTypes() {
            if (!AsTypesStart(out var parenthetical))
                return null;
            if (parenthetical && Check(TokenType.DM_RightParenthesis)) // as ()
                return DMValueType.Anything; // TODO: BYOND doesn't allow this for proc return types

            DMValueType type = DMValueType.Anything;

            do {
                Whitespace();
                type |= SingleAsType(out _);
                Whitespace();
            } while (Check(TokenType.DM_Bar));

            if (parenthetical) {
                ConsumeRightParenthesis();
            }

            return type;
        }

        /// <summary>
        /// AsTypes(), but can handle more complex types such as type paths
        /// </summary>
        private DMComplexValueType? AsComplexTypes() {
            if (!AsTypesStart(out var parenthetical))
                return null;
            if (parenthetical && Check(TokenType.DM_RightParenthesis)) // as ()
                return DMValueType.Anything; // TODO: BYOND doesn't allow this for proc return types

            DMValueType type = DMValueType.Anything;
            DreamPath? path = null;

            do {
                Whitespace();
                type |= SingleAsType(out var pathType, allowPath: true);
                Whitespace();

                if (pathType != null) {
                    if (path == null)
                        path = pathType;
                    else
                        Compiler.Emit(WarningCode.BadToken, CurrentLoc,
                            $"Only one type path can be used, ignoring {pathType}");
                }

            } while (Check(TokenType.DM_Bar));

            if (parenthetical) {
                ConsumeRightParenthesis();
            }

            return new(type, path);
        }

        private bool AsTypesStart(out bool parenthetical) {
            if (Check(TokenType.DM_As)) {
                Whitespace();
                parenthetical = Check(TokenType.DM_LeftParenthesis);
                return true;
            }

            parenthetical = false;
            return false;
        }

        private DMValueType SingleAsType(out DreamPath? path, bool allowPath = false) {
            Token typeToken = Current();

            if (!Check(new[] { TokenType.DM_Identifier, TokenType.DM_Null })) {
                // Proc return types
                path = Path()?.Path;
                if (allowPath) {
                    if (path is null) {
                        Compiler.Emit(WarningCode.BadToken, typeToken.Location, "Expected value type or path");
                    }

                    return DMValueType.Path;
                }

                Compiler.Emit(WarningCode.BadToken, typeToken.Location, "Expected value type");
                return 0;
            }

            path = null;
            switch (typeToken.Text) {
                case "anything": return DMValueType.Anything;
                case "null": return DMValueType.Null;
                case "text": return DMValueType.Text;
                case "obj": return DMValueType.Obj;
                case "mob": return DMValueType.Mob;
                case "turf": return DMValueType.Turf;
                case "num": return DMValueType.Num;
                case "message": return DMValueType.Message;
                case "area": return DMValueType.Area;
                case "color": return DMValueType.Color;
                case "file": return DMValueType.File;
                case "command_text": return DMValueType.CommandText;
                case "sound": return DMValueType.Sound;
                case "icon": return DMValueType.Icon;
                case "path": return DMValueType.Path;
                case "opendream_unimplemented": return DMValueType.Unimplemented;
                case "opendream_unsupported": return DMValueType.Unsupported;
                case "opendream_compiletimereadonly": return DMValueType.CompiletimeReadonly;
                case "opendream_noconstfold": return DMValueType.NoConstFold;
                default:
                    Emit(WarningCode.BadToken, typeToken.Location, $"Invalid value type '{typeToken.Text}'");
                    return 0;
            }
        }
}
