using System.Collections.Generic;

namespace MinecraftProtoNet.Physics.Shapes;

public interface IIndexMerger
{
    IList<double> GetList();
    bool ForMergedIndexes(IndexConsumer consumer);
    int Size();
}

public delegate bool IndexConsumer(int firstIndex, int secondIndex, int resultIndex);
