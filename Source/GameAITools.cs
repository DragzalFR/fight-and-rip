public partial class Game
{
    // Start new AI tools =================================================================

    private void UpdateStage()
    {
        if (Stage == GameStage.Early)
        {
            foreach (var unit in MyUnits)
            {
                double rangeToCheck = 2 + Math.Floor(MyGold / 10d);
                List<Position> bordersToCheck = null;
                for (var i = 0; i < rangeToCheck; i++)
                    bordersToCheck = unit.GetBorders(true);
                // reset the range checking
                unit.GetBorders(false);

                foreach (var border in bordersToCheck)
                {
                    if (IsInside(border) && Map[border.X, border.Y].IsOpponent)
                    {
                        Stage = GameStage.Mid;
                        break;
                    }
                }

                if (Stage == GameStage.Mid)
                    break;
            }
        }

        if (Stage == GameStage.Mid)
        {
            if (NeutralPositions.Count <= LATE_NEUTRAL_COUNT)
                Stage = GameStage.Late;
        }
    }

    private void FreePlace(Position position)
    {
        var firstUnit = Map[position.X, position.Y].OccupiedBy as Unit;
        if (firstUnit == null) return;

        var arounds = firstUnit.GetBorders().FindAll(p => !Map[p.X, p.Y].IsOccupied);
        if (arounds.Count > 0)
        {
            if (arounds.Exists(p => Map[p.X, p.Y].IsOpponent))
                Move(firstUnit, arounds.Find(p => Map[p.X, p.Y].IsOpponent));
            else if (arounds.Exists(p => Map[p.X, p.Y].IsNeutral))
                Move(firstUnit, arounds.Find(p => Map[p.X, p.Y].IsNeutral));
            else
                Move(firstUnit, arounds[0]);
        }
    }

    private void ApplyCut(in List<Position> positionsToCut)
    {
        foreach (var pos in positionsToCut)
        {
            int level = 1;
            while (level < 4 && !Train(level, pos))
                level++;

            if (level == 4)
                Console.Error.WriteLine("SHOULD NOT HAPPEN. In Game.ApplyCut : level == 4.");
        }
    }

    private void SendOneToCollect(ref List<Unit> unitsToMove)
    {
        if (unitsToMove.Where(u => u.Level == 1).ToList().Count == 0) return;

        if (NeutralPositions.Count <= LATE_NEUTRAL_COUNT)
        {
            Stage = GameStage.Late;
            return;
        }

        unitsToMove.Sort(delegate (Unit u1, Unit u2)
        {
            var dist1 = u1.Position.Dist(MyHq);
            var dist2 = u2.Position.Dist(MyHq);
            return (int)(dist1 - dist2);
        });

        Unit collector = unitsToMove.Where(u => u.Level == 1).ToList()[0];
        List<Position> targetsAroundCollector = collector.GetBorders().FindAll(p => Map[p.X, p.Y].IsNeutral);

        const int RANGE_CHECK = 5;
        int rangeCount = 0;

        while (targetsAroundCollector.Count == 0)
        {
            if (rangeCount++ > RANGE_CHECK)
            {
                Stage = GameStage.Late;
                return;
            }
            targetsAroundCollector = collector.GetBorders(true).FindAll(p => Map[p.X, p.Y].IsNeutral);
        }

        targetsAroundCollector.Sort(delegate (Position p1, Position p2)
        {
            double dist1 = p1.Dist(MyHq);
            double dist2 = p2.Dist(MyHq);
            return (int)(dist2 - dist1);
        });

        var collectorSteps = PathFinding(collector.Position, targetsAroundCollector[0]);
        Move(collector, collectorSteps[1]);
        unitsToMove.RemoveAt(0);
    }

    private void SendToCloserEmpty(ref List<Unit> unitsToMove)
    {
        List<KeyValuePair<Unit, Position>> moves = new List<KeyValuePair<Unit, Position>>();

        var targetPosition = NeutralPositions;
        targetPosition.AddRange(OpponentPositions.Where(p => !IsDefended(p) && (!Map[p.X, p.Y].IsOccupied)).ToList());

        while (unitsToMove.Count > 0 && targetPosition.Count > 0)
        {
            var currentUnit = unitsToMove[0];
            var arounds = currentUnit.GetBorders(true);
            arounds.Sort(delegate (Position p1, Position p2)
            {
                int dist1 = (int)p1.Dist(MyHq);
                int dist2 = (int)p2.Dist(MyHq);
                if (dist1 != dist2)
                    return dist2 - dist1;
                else
                    return (Math.Abs(p1.X - p1.Y) - Math.Abs(p2.X - p2.Y));
            });
            foreach (var around in arounds)
            {
                if (targetPosition.Contains(around))
                {
                    KeyValuePair<Unit, Position> move;
                    if (around.Dist(currentUnit.Position) > 1)
                    {
                        var steps = PathFinding(currentUnit.Position, around);
                        move = new KeyValuePair<Unit, Position>(currentUnit, steps[1]);
                    }
                    else
                        move = new KeyValuePair<Unit, Position>(currentUnit, around);

                    moves.Add(move);
                    targetPosition.Remove(around);
                    break;
                }
            }

            // If no move add,we reUp the unit.
            if (!moves.Exists(p => p.Key == currentUnit))
                unitsToMove.Add(currentUnit);
            unitsToMove.RemoveAt(0);
        }

        SortAndDoMoves(moves);

        if (targetPosition.Count <= LATE_NEUTRAL_COUNT)
            Stage = GameStage.Late;
    }

    private void TrainAroundLeader()
    {
        if (NeutralPositions.Count <= LATE_NEUTRAL_COUNT)
        {
            Stage = GameStage.Late;
            return;
        }

        var leader = new Unit { Owner = ME, Position = MyHq };
        int leaderDistance = HEIGHT * WIDTH;

        foreach (var myUnit in MyUnits)
        {
            int distance = PathFinding(myUnit.Position, OpponentHq).Count;
            if (distance < leaderDistance)
            {
                if (distance != leaderDistance || Math.Abs(leader.X - leader.Y) <= Math.Abs(myUnit.X - myUnit.Y))
                {
                    leader = myUnit;
                    leaderDistance = distance;
                }
            }
        }

        List<Position> targetsAroundLeader = leader.GetBorders().FindAll(p => Map[p.X, p.Y].IsNeutral);
        while (MyGold >= TRAIN_COST_LEVEL_1)
        {
            const int RANGE_CHECK = 3;
            int rangeCount = 0;
            while (targetsAroundLeader.Count == 0)
            {
                if (rangeCount++ > RANGE_CHECK) return;
                targetsAroundLeader = leader.GetBorders(true).FindAll(p => Map[p.X, p.Y].IsNeutral);
            }

            targetsAroundLeader.Sort(delegate (Position p1, Position p2) {
                var distToCenter1 = Math.Abs(p1.X - p1.Y);
                var distToCenter2 = Math.Abs(p2.X - p2.Y);
                return (int)(distToCenter1 - distToCenter2);
            });

            Train(1, targetsAroundLeader[0]);
            targetsAroundLeader.RemoveAt(0);
        }
    }

    private void MovesUnitLevelThree()
    {
        var unitsLevelThree = MyUnits.Where(u => u.Level == 3).ToList();
        unitsLevelThree.Sort(delegate (Unit u1, Unit u2)
        {
            var dist1 = u1.Position.Dist(MyHq);
            var dist2 = u2.Position.Dist(MyHq);
            return (int)(dist1 - dist2);
        });

        var opponentsTower = Buildings.Where(b => b.IsOpponent && b.IsTower).ToList();

        foreach (var unit in unitsLevelThree)
        {
            if (opponentsTower.Count == 0)
                Move(unit, OpponentHq);
            else
            {
                opponentsTower.Sort(delegate (Building b1, Building b2)
                {
                    var dist1 = b1.Position.Dist(unit.Position);
                    var dist2 = b2.Position.Dist(unit.Position);
                    return (int)(dist1 - dist2);
                });

                Move(unit, opponentsTower[0].Position);
                opponentsTower.RemoveAt(0);
            }
        }
    }
}
