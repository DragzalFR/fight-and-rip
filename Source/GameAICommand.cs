public partial class Game
{
    // Start add Commande =================================================================

    public void Wait()
    {
        Output.Append("WAIT;");
    }

    public bool CanGoOn(int level, Position target)
    {
        // Check if we can reach the position
        if (!ReachablePositions.Exists(p => p == target)) return false;

        Tile targetTile = Map[target.X, target.Y];

        if (targetTile.IsOwned && targetTile.IsOccupied) return false;

        // If the tile is opponent we check if we have the power to Go
        if (targetTile.IsOpponent)
        {
            Building opponentBuilding = targetTile.OccupiedBy as Building;
            Unit opponentUnit = targetTile.OccupiedBy as Unit;
            switch (level)
            {
                case 1:
                    if (IsDefended(target)) return false;
                    if (opponentBuilding != null && opponentBuilding.IsTower) return false;
                    if (opponentUnit != null) return false;
                    break;
                case 2:
                    if (IsDefended(target)) return false;
                    if (opponentBuilding != null && opponentBuilding.IsTower) return false;
                    if (opponentUnit != null && opponentUnit.Level > 1) return false;
                    break;
                case 3:
                    break;
            }
        }

        return true;
    }

    public bool Train(int level, Position position)
    {
        if (!CanGoOn(level, position)) return false;

        // Check if the position is in our perimeter
        if (!Map[position.X, position.Y].IsOwned)
        {
            // A position is valid to train if at least 
            // one active case around is owned
            bool isValid = false;

            var around = position.Arounds();
            foreach (var pos in around)
            {
                if (Map[pos.X, pos.Y].Active && Map[pos.X, pos.Y].IsOwned)
                    isValid = true;
            }

            if (!isValid) return false;
        }

        int cost = 0;
        switch (level)
        {
            case 1: cost = TRAIN_COST_LEVEL_1; break;
            case 2: cost = TRAIN_COST_LEVEL_2; break;
            case 3: cost = TRAIN_COST_LEVEL_3; break;
        }
        if (MyGold < cost) return false;
        MyGold -= cost;

        int income = 0;
        switch (level)
        {
            case 1: income -= UPKEEP_COST_LEVEL_1; break;
            case 2: income -= UPKEEP_COST_LEVEL_2; break;
            case 3: income -= UPKEEP_COST_LEVEL_3; break;
        }
        MyIncome -= income;

        Output.Append($"TRAIN {level} {position.X} {position.Y};");

        var id = Units.Count > 0 ? Units[Units.Count - 1].Id + 1 : 1;
        Units.Add(new Unit
        {
            Owner = ME,
            Id = id,
            Level = level,
            Position = position,
            IsMoved = true
        });
        Map[position.X, position.Y].Owner = ME;
        Map[position.X, position.Y].Active = true;
        Map[position.X, position.Y].OccupiedBy = Units[Units.Count - 1];

        UpdateActive();

        return true;
    }

    public bool Move(Unit unit, Position position)
    {
        if (unit.IsMoved) return false;

        if (!CanGoOn(unit.Level, position)) return false;

        Output.Append($"MOVE {unit.Id} {position.X} {position.Y};");

        unit.IsMoved = true;
        Map[unit.X, unit.Y].OccupiedBy = null;

        if (unit.Position.Dist(position) <= 1)
        {
            unit.Position = position;
            Map[position.X, position.Y].Owner = ME;
            Map[position.X, position.Y].Active = true;
            Map[position.X, position.Y].OccupiedBy = unit;
        }

        UpdateActive();

        return true;
    }

    private void UpdateActive()
    {
        UpdateActiveOf(MyHq);
        UpdateActiveOf(OpponentHq);
    }

    private void UpdateActiveOf(Position hq)
    {

        List<Position> fromHq = ReachablePositions.Where(p => Map[p.X, p.Y].Owner == Map[hq.X, hq.Y].Owner).ToList();
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
                    && !toCheck.Exists(p => p == around))
                {
                    toCheck.Add(around);
                }
            }
        }
    }

    public bool Build(string type, Position position)
    {
        // Check if we can reach the position
        if (!MyPositions.Exists(p => p == position)) return false;

        if (Map[position.X, position.Y].IsOccupied) return false;

        if (!Map[position.X, position.Y].IsOwned) return false;

        if (type == "MINE")
        {
            if (MyGold < BUILD_COST_MINE) return false;
            if (!Map[position.X, position.Y].HasMineSpot) return false;
        }
        if (type == "TOWER")
        {
            if (MyGold < BUILD_COST_TOWER) return false;
            if (Map[position.X, position.Y].HasMineSpot) return false;
        }

        Output.Append($"BUILD {type} {position.X} {position.Y};");

        if (type == "TOWER")
        {
            MyGold -= BUILD_COST_TOWER;
            Buildings.Add(new Building
            {
                Owner = ME,
                Position = position,
                Type = type == "TOWER" ? BuildingType.Tower : BuildingType.Mine,
            });
        }

        if (type == "MINE")
        {
            MyGold -= BUILD_COST_MINE;
            Buildings.Add(new Building
            {
                Owner = ME,
                Position = position,
                Type = type == "TOWER" ? BuildingType.Tower : BuildingType.Mine,
            });
        }

        Map[position.X, position.Y].OccupiedBy = Buildings[Buildings.Count - 1];

        return true;
    }
}