namespace DMCompiler.Compiler.DM.AST;

public abstract class DMASTExpressionConstant(Location location) : DMASTExpression(location);

public sealed class DMASTConstantInteger(Location location, int value) : DMASTExpressionConstant(location) {
    public readonly int Value = value;

    public override void Visit(DMASTVisitor visitor) {
        visitor.VisitInteger(this);
    }
}

public sealed class DMASTConstantFloat(Location location, float value) : DMASTExpressionConstant(location) {
    public readonly float Value = value;

    public override void Visit(DMASTVisitor visitor) {
        visitor.VisitFloat(this);
    }
}

public sealed class DMASTConstantString(Location location, string value) : DMASTExpressionConstant(location) {
    public readonly string Value = value;

    public override void Visit(DMASTVisitor visitor) {
        visitor.VisitString(this);
    }
}

public sealed class DMASTConstantResource(Location location, string path) : DMASTExpressionConstant(location) {
    public readonly string Path = path;

    public override void Visit(DMASTVisitor visitor) {
        visitor.VisitResource(this);
    }
}

public sealed class DMASTConstantNull(Location location) : DMASTExpressionConstant(location) {
    public override void Visit(DMASTVisitor visitor) {
        visitor.VisitNull(this);
    }
}

public sealed class DMASTConstantPath(Location location, DMASTPath value) : DMASTExpressionConstant(location) {
    public readonly DMASTPath Value = value;

    public override void Visit(DMASTVisitor visitor) {
        visitor.VisitConstantPath(this);
    }
}

public sealed class DMASTUpwardPathSearch(Location location, DMASTExpressionConstant path, DMASTPath search)
    : DMASTExpressionConstant(location) {
    public readonly DMASTExpressionConstant Path = path;
    public readonly DMASTPath Search = search;

    public override void Visit(DMASTVisitor visitor) {
        visitor.VisitUpwardPathSearch(this);
    }
}
