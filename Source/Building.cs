public class Building : Entity
{
    public BuildingType Type;

    public bool IsHq => Type == BuildingType.Hq;
    public bool IsTower => Type == BuildingType.Tower;
    public bool IsMine => Type == BuildingType.Mine;

    public override string ToString() => $"Building => {base.ToString()} Type: {Type}";
}