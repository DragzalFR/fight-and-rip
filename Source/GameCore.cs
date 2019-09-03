public partial class Game
{
    public GameStage Stage = GameStage.Early;

    public readonly List<Building> Buildings = new List<Building>();

    public readonly Tile[,] Map = new Tile[WIDTH, HEIGHT];
    public readonly StringBuilder Output = new StringBuilder();

    public readonly List<Position> MineSpots = new List<Position>();

    public int MyGold;
    public int MyIncome;
    public Team MyTeam;

    public int OpponentGold;
    public int OpponentIncome;
    public int Turn;
    public List<Unit> Units = new List<Unit>();

    public List<Unit> MyUnits => Units.Where(u => u.IsOwned).ToList();
    public List<Unit> OpponentUnits => Units.Where(u => u.IsOpponent).ToList();

    public Position MyHq => MyTeam == Team.Fire ? (0, 0) : (11, 11);
    public Position OpponentHq => MyTeam == Team.Fire ? (11, 11) : (0, 0);
    public int Offset => MyTeam == Team.Fire ? 1 : -1;

    public List<Position> ReachablePositions = new List<Position>();
    public List<Position> MyPositions => ReachablePositions.Where(p => Map[p.X, p.Y].IsOwned).ToList();
    public List<Position> OpponentPositions => ReachablePositions.Where(p => Map[p.X, p.Y].IsOpponent).ToList();
    public List<Position> NeutralPositions => ReachablePositions.Where(p => Map[p.X, p.Y].IsNeutral).ToList();

    private char[,] MapInput = new char[WIDTH, HEIGHT];
    private List<string> OutputsByTurn;

    public void Init()
    {
        for (var y = 0; y < HEIGHT; y++)
            for (var x = 0; x < WIDTH; x++)
            {
                Map[x, y] = new Tile
                {
                    Position = (x, y)
                };
            }

        var numberMineSpots = int.Parse(Console.ReadLine());
        for (var i = 0; i < numberMineSpots; i++)
        {
            var inputs = Console.ReadLine().Split(' ');
            MineSpots.Add((int.Parse(inputs[0]), int.Parse(inputs[1])));
        }
    }

    public void Update()
    {
        Units.Clear();
        Buildings.Clear();

        ReachablePositions.Clear();

        Output.Clear();

        // --------------------------------------

        MyGold = int.Parse(Console.ReadLine());
        MyIncome = int.Parse(Console.ReadLine());
        OpponentGold = int.Parse(Console.ReadLine());
        OpponentIncome = int.Parse(Console.ReadLine());

        // Read Map
        for (var y = 0; y < HEIGHT; y++)
        {
            var line = Console.ReadLine();
            for (var x = 0; x < WIDTH; x++)
            {
                var c = line[x] + "";
                Map[x, y].IsWall = c == "#";
                Map[x, y].Active = "OX".Contains(c);
                Map[x, y].Owner = c.ToLower() == "o" ? ME : c.ToLower() == "x" ? OPPONENT : NEUTRAL;
                Map[x, y].HasMineSpot = MineSpots.Count(spot => spot == (x, y)) > 0;

                Map[x, y].OccupiedBy = null;

                Position p = (x, y);
                if (!Map[x, y].IsWall)
                    ReachablePositions.Add(p);

                MapInput[x, y] = line[x];
            }
        }

        // Read Buildings
        var buildingCount = int.Parse(Console.ReadLine());
        for (var i = 0; i < buildingCount; i++)
        {
            var inputs = Console.ReadLine().Split(' ');
            Buildings.Add(new Building
            {
                Owner = int.Parse(inputs[0]),
                Type = (BuildingType)int.Parse(inputs[1]),
                Position = (int.Parse(inputs[2]), int.Parse(inputs[3]))
            });

            Map[Buildings[i].X, Buildings[i].Y].OccupiedBy = Buildings[i];
        }

        // Read Units
        var unitCount = int.Parse(Console.ReadLine());
        for (var i = 0; i < unitCount; i++)
        {
            var inputs = Console.ReadLine().Split(' ');
            Units.Add(new Unit
            {
                Owner = int.Parse(inputs[0]),
                Id = int.Parse(inputs[1]),
                Level = int.Parse(inputs[2]),
                Position = (int.Parse(inputs[3]), int.Parse(inputs[4]))
            });

            Map[Units[i].X, Units[i].Y].OccupiedBy = Units[i];
        }

        // --------------------------------

        // Get Team
        MyTeam = Buildings.Find(b => b.IsHq && b.IsOwned).Position == (0, 0) ? Team.Fire : Team.Ice;

        // Usefull for symmetric AI
        ReachablePositions.Sort(delegate (Position p1, Position p2)
        {
            double x = p1.Dist(OpponentHq);
            double y = p2.Dist(OpponentHq);
            return (int)(x - y);
        });

        // --------------------------------

        UpdateStage();

        // Debug
        Debug();
    }

    [Conditional("SHOW_DEBUG")]
    public void Debug()
    {
        Console.Error.WriteLine($"Turn: {Turn}");
        Console.Error.WriteLine($"My team: {MyTeam}");
        Console.Error.WriteLine($"My gold: {MyGold} (+{MyIncome})");
        Console.Error.WriteLine($"Opponent gold: {OpponentGold} (+{OpponentIncome})");

        Console.Error.WriteLine("=====");
        foreach (var b in Buildings) Console.Error.WriteLine(b);
        foreach (var u in Units) Console.Error.WriteLine(u);

        Console.Error.WriteLine("=====");
        for (var y = 0; y < HEIGHT; y++)
        {
            Console.Error.WriteLine();
            for (var x = 0; x < WIDTH; x++)
                Console.Error.Write(MapInput[x, y]);
        }
        Console.Error.WriteLine();
    }

}