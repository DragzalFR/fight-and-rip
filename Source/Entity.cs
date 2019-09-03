public class Entity
{
    public int Owner;
    public Position Position;

    public bool IsOwned => Owner == ME;
    public bool IsOpponent => Owner == OPPONENT;

    public int X => Position.X;
    public int Y => Position.Y;

    public override string ToString() => $"Owner: {Owner} Position: {Position}";
}