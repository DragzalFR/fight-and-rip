public partial class Game
{
    // Start Utilitary ====================================================================

    public bool IsMyBorder(Position position)
    {
        if (!Map[position.X, position.Y].IsOwned || !Map[position.X, position.Y].Active) return false;

        foreach (var around in position.Arounds())
        {
            if (Map[around.X, around.Y].IsOpponent) return true;
        }

        return false;
    }

    public bool IsDefended(Position position)
    {

        Building building = Map[position.X, position.Y].OccupiedBy as Building;
        if (building != null && building.IsTower)
            return true;
        else if (!Map[position.X, position.Y].Active)
            return false;

        foreach (var around in position.Arounds())
        {
            if (Map[around.X, around.Y].Owner == Map[position.X, position.Y].Owner)
            {
                building = Map[around.X, around.Y].OccupiedBy as Building;
                if (building != null && building.IsTower) return true;
            }

        }

        return false;
    }

    private int CalculCutBenefit(Position hq, List<Position> limit)
    {
        if (limit.Exists(p => p == hq)) return int.MaxValue;

        List<Position> fromHq = ReachablePositions.Where(p => Map[p.X, p.Y].Owner == Map[hq.X, hq.Y].Owner).ToList();
        fromHq.RemoveAll(p => !Map[p.X, p.Y].Active);
        foreach (var pos in fromHq)
            Map[pos.X, pos.Y].Active = false;

        List<Position> visited = new List<Position>();
        List<Position> toCheck = new List<Position>();
        toCheck.Add(hq);

        while (toCheck.Count > 0)
        {
            Position current = toCheck[0];
            toCheck.Remove(current);
            visited.Add(current);
            Map[current.X, current.Y].Active = true;

            var arounds = current.Arounds();
            arounds.RemoveAll(p => Map[p.X, p.Y].Owner != Map[hq.X, hq.Y].Owner);
            foreach (var around in arounds)
            {
                if (!visited.Exists(p => p == around)
                    && !toCheck.Exists(p => p == around)
                    && !limit.Exists(p => p == around))
                {
                    toCheck.Add(around);
                }
            }
        }

        int benefit = 0;
        var positionsCut = fromHq.ToList();
        positionsCut.RemoveAll(p => Map[p.X, p.Y].Active);
        foreach (var pos in positionsCut)
        {
            Building tileBuilding = Map[pos.X, pos.Y].OccupiedBy as Building;
            Unit tileUnit = Map[pos.X, pos.Y].OccupiedBy as Unit;
            if (tileBuilding != null)
            {
                benefit += 1;

                if (tileBuilding.IsMine)
                    benefit += BUILD_COST_MINE / 2 + 4;
                if (tileBuilding.IsTower)
                {
                    if (limit.Exists(p => p == pos))
                        benefit += BUILD_COST_TOWER;
                    else
                        benefit += BUILD_COST_TOWER / 3;
                }

            }
            else if (tileUnit != null)
            {
                switch (tileUnit.Level)
                {
                    case 1:
                        benefit += TRAIN_COST_LEVEL_1 - UPKEEP_COST_LEVEL_1;
                        break;
                    case 2:
                        benefit += TRAIN_COST_LEVEL_2 - UPKEEP_COST_LEVEL_2;
                        break;
                    case 3:
                        if (tileUnit.GetBorders().Exists(p => IsDefended(p)))
                            benefit += BUILD_COST_TOWER;
                        benefit += TRAIN_COST_LEVEL_3 - UPKEEP_COST_LEVEL_3;
                        break;
                }
            }
            else
                benefit += 1;
        }

        foreach (var pos in fromHq)
            Map[pos.X, pos.Y].Active = true;

        foreach (var pos in limit)
        {
            int level = 1;
            while (!CanGoOn(level, pos) && level < 3)
            {
                level++;
            }

            switch (level)
            {
                case 1:
                    benefit -= UPKEEP_COST_LEVEL_1;
                    break;
                case 2:
                    benefit -= UPKEEP_COST_LEVEL_2;
                    break;
                case 3:
                    benefit -= UPKEEP_COST_LEVEL_3 / 2;
                    break;
            }
        }

        return positionsCut.Count == limit.Count ? 0 : benefit;
    }

    public List<Position> PathFinding(Position start, Position destination)
    {
        bool debug = false;

        var fieldCost = new float[WIDTH, HEIGHT];
        for (var y = 0; y < HEIGHT; y++)
            for (var x = 0; x < WIDTH; x++)
                fieldCost[x, y] = Map[x, y].IsWall ? -1 : HEIGHT * WIDTH;

        var cameFrom = new Position[WIDTH, HEIGHT];

        List<Position> exploring = new List<Position>();

        fieldCost[start.X, start.Y] = 0;
        exploring.Add(start);

        while (exploring.Count > 0)
        {
            Position position = (exploring[0].X, exploring[0].Y);

            var currentSteps = exploring.Where(p => p.Dist(destination) == position.Dist(destination)).ToList();
            currentSteps.Sort(delegate (Position p1, Position p2) {
                int i = Math.Abs(p1.X - p1.Y);
                int j = Math.Abs(p2.X - p2.Y);
                return i - j;
            });
            foreach (var current in currentSteps)
            {
                float costToNext = fieldCost[current.X, current.Y] + 1;
                var arounds = current.Arounds();
                foreach (var next in arounds)
                {
                    if (next == destination)
                    {
                        cameFrom[next.X, next.Y] = current;
                        fieldCost[next.X, next.Y] = costToNext;
                        exploring.Clear();
                        break;
                    }

                    if (costToNext < fieldCost[next.X, next.Y])
                    {
                        exploring.Remove(next);
                        fieldCost[next.X, next.Y] = costToNext;
                        cameFrom[next.X, next.Y] = current;
                        exploring.Add(next);
                    }
                }
                exploring.Remove(current);
            }

            exploring.Sort(delegate (Position p1, Position p2)
            {
                double x = p1.Dist(destination);
                double y = p2.Dist(destination);
                return (int)(x - y);
            });
        }

        if (debug)
            for (var y = 0; y < HEIGHT; y++)
            {
                for (var x = 0; x < WIDTH; x++)
                {
                    if (fieldCost[x, y] == -1)
                        Console.Error.Write("## ");
                    else if (fieldCost[x, y] == 144)
                        Console.Error.Write("?? ");
                    else if (fieldCost[x, y] < 10)
                        Console.Error.Write(" " + (int)fieldCost[x, y] + " ");
                    else
                        Console.Error.Write((int)fieldCost[x, y] + " ");
                }
                Console.Error.WriteLine();
            }

        var result = new List<Position>();
        Position step = destination;
        while (step != start)
        {
            result.Add(step);
            if (debug)
                Console.Error.WriteLine(step);
            step = cameFrom[step.X, step.Y];

            if (step == null)
                return null;
        }
        result.Add(start);
        result.Reverse();
        return result;
    }

    // Try to optimise movement in case of target is occupied. 
    public void SortAndDoMoves(List<KeyValuePair<Unit, Position>> moves)
    {
        if (moves.Count == 0) return;

        var movesWithPathFinding = new List<KeyValuePair<Unit, Position>>();
        Unit lastUnit = moves[moves.Count - 1].Key;
        bool changed = false;
        while (moves.Count > 0)
        {
            Position target = moves[0].Value;
            Unit unitToMove = moves[0].Key;
            var currentPair = moves[0];
            moves.RemoveAt(0);

            if (Map[target.X, target.Y].IsOccupied && Map[target.X, target.Y].IsOwned)
            {
                if (lastUnit == unitToMove)
                {
                    if (!changed) break;
                    else changed = false;
                }

                moves.Add(currentPair);
            }
            else
            {
                if (lastUnit == unitToMove)
                {
                    if (moves.Count > 0)
                        lastUnit = moves[moves.Count - 1].Key;
                    changed = false;
                }
                else
                    changed = true;

                if (target.Dist(unitToMove.Position) > 1)
                {
                    Console.Error.WriteLine("Distance of move > 1");
                    Console.Error.WriteLine(unitToMove.Position + " => " + target);
                    movesWithPathFinding.Add(currentPair);
                }
                // We know the tile can't be owned due to a previous if.
                else if (Map[target.X, target.Y].IsOccupied && unitToMove.Level != 3)
                {
                    Building building = Map[target.X, target.Y].OccupiedBy as Building;
                    Unit unit = Map[target.X, target.Y].OccupiedBy as Unit;
                    if (building != null && !building.IsTower)
                    {
                        Move(unitToMove, target);
                    }
                    else if (unit != null && unitToMove.Level > unit.Level)
                    {
                        Move(unitToMove, target);
                    }
                }
                else
                    Move(unitToMove, target);
            }
        }

        foreach (var pair in movesWithPathFinding)
            Move(pair.Key, pair.Value);
    }
}