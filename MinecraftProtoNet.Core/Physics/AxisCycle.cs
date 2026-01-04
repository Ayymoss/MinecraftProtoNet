namespace MinecraftProtoNet.Core.Physics;

public abstract class AxisCycle
{
    public static readonly AxisCycle None = new NoneCycle();
    public static readonly AxisCycle Forward = new ForwardCycle();
    public static readonly AxisCycle Backward = new BackwardCycle();
    
    public static readonly AxisCycle[] Values = { None, Forward, Backward };

    public abstract int Cycle(int x, int y, int z, Axis axis);
    public abstract double Cycle(double x, double y, double z, Axis axis);
    public abstract Axis Cycle(Axis axis);
    public abstract AxisCycle Inverse();

    public static AxisCycle Between(Axis from, Axis to)
    {
        return Values[MathHelpers.FloorMod(to - from, 3)];
    }

    private class NoneCycle : AxisCycle
    {
        public override int Cycle(int x, int y, int z, Axis axis) => axis.Choose(x, y, z);
        public override double Cycle(double x, double y, double z, Axis axis) => axis.Choose(x, y, z);
        public override Axis Cycle(Axis axis) => axis;
        public override AxisCycle Inverse() => this;
    }

    private class ForwardCycle : AxisCycle
    {
        public override int Cycle(int x, int y, int z, Axis axis) => axis.Choose(z, x, y);
        public override double Cycle(double x, double y, double z, Axis axis) => axis.Choose(z, x, y);
        public override Axis Cycle(Axis axis) => AxisExtensions.Values[MathHelpers.FloorMod((int)axis + 1, 3)];
        public override AxisCycle Inverse() => Backward;
    }

    private class BackwardCycle : AxisCycle
    {
        public override int Cycle(int x, int y, int z, Axis axis) => axis.Choose(y, z, x);
        public override double Cycle(double x, double y, double z, Axis axis) => axis.Choose(y, z, x);
        public override Axis Cycle(Axis axis) => AxisExtensions.Values[MathHelpers.FloorMod((int)axis - 1, 3)];
        public override AxisCycle Inverse() => Forward;
    }
}

file static class MathHelpers
{
    public static int FloorMod(int x, int y)
    {
        int r = x % y;
        if ((x ^ y) < 0 && r != 0) r += y;
        return r;
    }
}
