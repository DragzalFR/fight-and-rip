public class Position
{
    public int X;
    public int Y;

    public static implicit operator Position(ValueTuple<int, int> cell) => new Position
    {
        X = cell.Item1,
        Y = cell.Item2
    };

    public override string ToString() => $"({X},{Y})";

    public static bool operator ==(Position obj1, Position obj2)
    {
        if (object.ReferenceEquals(null, obj1))
            return object.ReferenceEquals(null, obj2);
        return obj1.Equals(obj2);
    }

    public static bool operator !=(Position obj1, Position obj2)
    {
        if (object.ReferenceEquals(null, obj1))
            return !object.ReferenceEquals(null, obj2);
        return !obj1.Equals(obj2);
    }

    public override bool Equals(object obj) => Equals((Position)obj);

    protected bool Equals(Position other)
    {
        if (object.ReferenceEquals(other, null))
            return false;
        return (X == other.X && Y == other.Y);
    }

    public double Dist(Position p) => Math.Abs(X - p.X) + Math.Abs(Y - p.Y);

    public List<Position> Arounds(bool includeSelf = false)
    {
        var result = new List<Position>();

        Position up = (X, Y - 1);
        Position down = (X, Y + 1);
        Position left = (X - 1, Y);
        Position right = (X + 1, Y);

        if (IsInside(up)) result.Add(up);
        if (IsInside(down)) result.Add(down);
        if (IsInside(left)) result.Add(left);
        if (IsInside(right)) result.Add(right);
        if (includeSelf) result.Add((X, Y));

        return result;
    }
}