public class Unit : Entity
{
    public int Id;
    public int Level;

    public bool IsMoved = false;

    public override string ToString() => $"Unit => {base.ToString()} Id: {Id} Level: {Level}";

    public static bool operator ==(Unit obj1, Unit obj2)
    {
        if (object.ReferenceEquals(null, obj1))
            return object.ReferenceEquals(null, obj2);
        return obj1.Equals(obj2);
    }

    public static bool operator !=(Unit obj1, Unit obj2)
    {
        if (object.ReferenceEquals(null, obj1))
            return !object.ReferenceEquals(null, obj2);
        return !obj1.Equals(obj2);
    }

    public override bool Equals(object obj) => Equals((Unit)obj);

    protected bool Equals(Unit other)
    {
        if (object.ReferenceEquals(other, null))
            return false;
        return (Id == other.Id);
    }

    private List<Position> borderPositions;
    private int borderRange = 0;

    public List<Position> GetBorders(bool increment = false)
    {
        if (!increment)
        {
            borderRange = 0;
            return Position.Arounds();
        }
        else
        {
            if (borderRange == 0)
                borderPositions = Position.Arounds();
            else
            {
                var nextBorderPositions = new List<Position>();
                foreach (var border in borderPositions)
                    nextBorderPositions.AddRange(border.Arounds());
                nextBorderPositions.RemoveAll(p => p.Dist(Position) <= borderRange);

                borderPositions = nextBorderPositions;
            }

            borderRange++;
            return borderPositions;
        }
    }
}