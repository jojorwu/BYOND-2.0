namespace DMCompiler.Compiler.DM.AST;

public sealed class DMASTSleep(Location location, DMASTExpression delay) : DMASTProcStatement(location) {
    public readonly DMASTExpression Delay = delay;

    public override void Visit(DMASTVisitor visitor) {
        visitor.VisitSleep(this);
    }
}
