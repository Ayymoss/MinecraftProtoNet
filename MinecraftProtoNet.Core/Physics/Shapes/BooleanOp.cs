namespace MinecraftProtoNet.Core.Physics.Shapes;

public delegate bool BooleanOp(bool first, bool second);

public static class BooleanOps
{
    public static readonly BooleanOp False = (f, s) => false;
    public static readonly BooleanOp NotOr = (f, s) => !f && !s;
    public static readonly BooleanOp OnlySecond = (f, s) => s && !f;
    public static readonly BooleanOp NotFirst = (f, s) => !f;
    public static readonly BooleanOp OnlyFirst = (f, s) => f && !s;
    public static readonly BooleanOp NotSecond = (f, s) => !s;
    public static readonly BooleanOp NotSame = (f, s) => f != s;
    public static readonly BooleanOp NotAnd = (f, s) => !f || !s;
    public static readonly BooleanOp And = (f, s) => f && s;
    public static readonly BooleanOp Same = (f, s) => f == s;
    public static readonly BooleanOp Second = (f, s) => s;
    public static readonly BooleanOp Causes = (f, s) => !f || s;
    public static readonly BooleanOp First = (f, s) => f;
    public static readonly BooleanOp CausedBy = (f, s) => f || !s;
    public static readonly BooleanOp Or = (f, s) => f || s;
    public static readonly BooleanOp True = (f, s) => true;
}
