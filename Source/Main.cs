public enum BuildingType
{
    Hq,
    Mine,
    Tower
}

public enum Team
{
    Fire,
    Ice
}

public enum GameStage
{
    Early,
    Mid,
    Late,
    End
}

private const int WIDTH = 12;
private const int HEIGHT = 12;
private static bool IsInside(int x, int y)
{
    return x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT;
}
private static bool IsInside(Position p)
{
    return p.X >= 0 && p.X < WIDTH && p.Y >= 0 && p.Y < HEIGHT;
}

private const int ME = 0;
private const int OPPONENT = 1;
private const int NEUTRAL = -1;

private const int TRAIN_COST_LEVEL_1 = 10;
private const int TRAIN_COST_LEVEL_2 = 20;
private const int TRAIN_COST_LEVEL_3 = 30;

private const int UPKEEP_COST_LEVEL_1 = 1;
private const int UPKEEP_COST_LEVEL_2 = 4;
private const int UPKEEP_COST_LEVEL_3 = 20;

private const int BUILD_COST_TOWER = 15;
private const int BUILD_COST_MINE = 20;

private const int LATE_NEUTRAL_COUNT = 2;

private static void Main()
{
    var game = new Game();
    game.Init();

    long maxTimeSpend = 0;
    bool first = true;

    // game loop
    while (true)
    {
        game.Update();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        // the code that you want to measure comes here
        game.Solve();

        watch.Stop();
        Console.Error.WriteLine();
        Console.Error.WriteLine("Timer check!");
        var elapsedMs = watch.ElapsedMilliseconds;
        if (first)
        {
            Console.Error.WriteLine("First turn : " + elapsedMs);
            first = false;
        }
        else if (elapsedMs > maxTimeSpend)
        {
            maxTimeSpend = elapsedMs;
            // Console.Error.WriteLine("TimeSpend : " + elapsedMs);
        }
        Console.Error.WriteLine("TimeSpend : " + maxTimeSpend);

        Console.WriteLine(game.Output.ToString());
    }
}