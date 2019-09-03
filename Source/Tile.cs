public class Tile
{
    public bool Active;
    public bool HasMineSpot;
    public bool IsWall;

    public Entity OccupiedBy = null;

    public int Owner = NEUTRAL;

    public Position Position;
    public int X => Position.X;
    public int Y => Position.Y;

    public bool IsOwned => Owner == ME;
    public bool IsOpponent => Owner == OPPONENT;
    public bool IsNeutral => Owner == NEUTRAL && !IsWall;

    public bool IsOccupied => OccupiedBy != null;

    public Tile() { }

    public Tile(Tile tile)
    {
        Active = tile.Active;
        HasMineSpot = tile.HasMineSpot;
        IsWall = tile.IsWall;
        OccupiedBy = tile.OccupiedBy;
        Owner = tile.Owner;
        Position = tile.Position;
    }

}