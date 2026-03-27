using System.Collections.Generic;
using System.Linq;
using DMCompiler.Compiler.DM.AST;

namespace DMCompiler.Compiler.DM;

public partial class DMParser
{
        private DMASTBlockInner? Block() {
            Token beforeBlockToken = Current();
            bool hasNewline = Newline();

            DMASTBlockInner? block = BracedBlock();
            block ??= IndentedBlock();

            if (block == null && hasNewline) {
                ReuseToken(beforeBlockToken);
            }

            return block;
        }

        private DMASTBlockInner? BracedBlock() {
            var loc = Current().Location;
            if (Check(TokenType.DM_LeftCurlyBracket)) {
                Whitespace();
                Newline();
                bool isIndented = Check(TokenType.DM_Indent);
                List<DMASTStatement>? blockInner = BlockInner();
                if (isIndented) Check(TokenType.DM_Dedent);
                Newline();
                Consume(TokenType.DM_RightCurlyBracket, "Expected '}'");

                return new DMASTBlockInner(loc, blockInner?.ToArray() ?? []);
            }

            return null;
        }

        private DMASTBlockInner? IndentedBlock() {
            var loc = Current().Location;
            if (Check(TokenType.DM_Indent)) {
                List<DMASTStatement>? blockInner = BlockInner();

                if (blockInner != null) {
                    Newline();
                    Consume(TokenType.DM_Dedent, "Expected dedent");

                    return new DMASTBlockInner(loc, blockInner.ToArray());
                }
            }

            return null;
        }

        private DMASTProcBlockInner? ProcBlock() {
            Token beforeBlockToken = Current();
            bool hasNewline = Newline();

            DMASTProcBlockInner? procBlock = BracedProcBlock();
            procBlock ??= IndentedProcBlock();

            if (procBlock == null && hasNewline) {
                ReuseToken(beforeBlockToken);
            }

            return procBlock;
        }

        private DMASTProcBlockInner? BracedProcBlock() {
            var loc = Current().Location;
            if (Check(TokenType.DM_LeftCurlyBracket)) {
                DMASTProcBlockInner? block;

                Whitespace();
                Newline();
                if (Current().Type == TokenType.DM_Indent) {
                    block = IndentedProcBlock();
                    Newline();
                    Consume(TokenType.DM_RightCurlyBracket, "Expected '}'");
                } else {
                    List<DMASTProcStatement> statements = new();
                    List<DMASTProcStatement> setStatements = new(); // set statements are weird and must be held separately.

                    do {
                        (List<DMASTProcStatement>? stmts, List<DMASTProcStatement>? setStmts) = ProcBlockInner(); // Hope you understand tuples
                        if (stmts is not null) statements.AddRange(stmts);
                        if (setStmts is not null) setStatements.AddRange(setStmts);

                        if (!Check(TokenType.DM_RightCurlyBracket)) {
                            Emit(WarningCode.BadToken, "Expected end of braced block");
                            Check(TokenType.DM_Dedent); // Have to do this ensure that the current token will ALWAYS move forward,
                                                        // and not get stuck once we reach this branch!
                            LocateNextStatement();
                            Delimiter();
                        } else {
                            break;
                        }
                    } while (true);

                    block = new DMASTProcBlockInner(loc, statements.ToArray(), setStatements.ToArray());
                }

                return block;
            }

            return null;
        }

        private DMASTProcBlockInner? IndentedProcBlock() {
            var loc = Current().Location;
            if (Check(TokenType.DM_Indent)) {
                List<DMASTProcStatement> statements = new();
                List<DMASTProcStatement> setStatements = new(); // set statements are weird and must be held separately.

                do {
                    (List<DMASTProcStatement>? statements, List<DMASTProcStatement>? setStatements) blockInner = ProcBlockInner();
                    if (blockInner.statements is not null)
                        statements.AddRange(blockInner.statements);
                    if (blockInner.setStatements is not null)
                        setStatements.AddRange(blockInner.setStatements);

                    if (!Check(TokenType.DM_Dedent)) {
                        Emit(WarningCode.BadToken, "Expected end of proc statement");
                        LocateNextStatement();
                        Delimiter();
                    } else {
                        break;
                    }
                } while (true);

                return new DMASTProcBlockInner(loc, statements.ToArray(), setStatements.ToArray());
            }

            return null;
        }

        private (List<DMASTProcStatement>?, List<DMASTProcStatement>?) ProcBlockInner() {
            List<DMASTProcStatement> procStatements = new();
            List<DMASTProcStatement> setStatements = new(); // We have to store them separately because they're evaluated first

            DMASTProcStatement? statement;
            do {
                Whitespace();
                statement = ProcStatement();

                if (statement is not null) {
                    Whitespace();
                    if(statement.IsAggregateOr<DMASTProcStatementSet>())
                        setStatements.Add(statement);
                    else
                        procStatements.Add(statement);
                }
            } while (Delimiter() || statement is DMASTProcStatementLabel);
            Whitespace();

            return (procStatements.Count > 0 ? procStatements : null, setStatements.Count > 0 ? setStatements : null);
        }
}
