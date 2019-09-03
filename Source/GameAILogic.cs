public partial class Game
{
    // Start new AI =======================================================================

    private bool TryToWin()
    {
        var myBorders = MyPositions.Where(p => IsMyBorder(p)).ToList();

        foreach (var border in myBorders)
        {
            var steps = PathFinding(border, OpponentHq);
            steps.RemoveAt(0);

            if (steps.Exists(p => Map[p.X, p.Y].IsOwned && Map[p.X, p.Y].Active)) continue;

            if (steps.Count * TRAIN_COST_LEVEL_1 > MyGold) continue;

            int cost = 0;
            steps.ForEach(delegate (Position p)
            {
                if (Map[p.X, p.Y].IsOwned)
                    cost += 0;
                else if (IsDefended(p))
                {
                    int index = steps.IndexOf(p);
                    if (index == 0)
                        cost += TRAIN_COST_LEVEL_3;
                    else
                    {
                        var arounds = p.Arounds(true);
                        arounds.Remove(steps[index - 1]);
                        arounds.RemoveAll(pA => !(Map[pA.X, pA.Y].OccupiedBy is Building));
                        arounds.RemoveAll(pA => !(Map[pA.X, pA.Y].OccupiedBy as Building).IsTower);
                        if (arounds.Count > 0)
                            cost += TRAIN_COST_LEVEL_3;
                        else
                            cost += TRAIN_COST_LEVEL_1;
                    }
                }
                else if (!Map[p.X, p.Y].IsOccupied)
                    cost += TRAIN_COST_LEVEL_1;
                else if (Map[p.X, p.Y].OccupiedBy is Unit)
                {
                    switch ((Map[p.X, p.Y].OccupiedBy as Unit).Level)
                    {
                        case 1:
                            cost += TRAIN_COST_LEVEL_2;
                            break;
                        case 2:
                            cost += TRAIN_COST_LEVEL_3;
                            break;
                        case 3:
                            cost += TRAIN_COST_LEVEL_3;
                            break;
                    }
                }
                else
                {
                    if ((Map[p.X, p.Y].OccupiedBy as Building).IsTower)
                        cost += TRAIN_COST_LEVEL_3;
                    else
                        cost += TRAIN_COST_LEVEL_1;
                }
            });

            if (cost <= MyGold)
            {
                Output.Clear();
                Wait();
                foreach (var p in steps)
                    Console.Error.Write(p + " - ");
                ApplyCut(in steps);
                Console.Error.WriteLine(Output);
                return true;
            }

        }

        return false;
    }

    private void AIMovesToScout()
    {
        if (MyUnits.Count == 0) return;

        var unitsToMove = MyUnits;

        Unit scout = unitsToMove[unitsToMove.Count - 1];
        var scoutSteps = PathFinding(scout.Position, OpponentHq);

        Move(scout, scoutSteps[1]);
        unitsToMove.Remove(scout);

        SendOneToCollect(ref unitsToMove);
        SendToCloserEmpty(ref unitsToMove);
    }

    private void AITrainsToScout()
    {
        if (MyGold < TRAIN_COST_LEVEL_1) return;

        var steps = PathFinding(MyHq, OpponentHq);

        int mid = (int)Math.Floor((steps.Count - 1) / 2d);

        for (var i = 1; i < steps.Count; i++)
        {
            if (!Map[steps[i].X, steps[i].Y].Active)
                Train(1, steps[i]);

            if (i == mid)
                Stage = GameStage.Mid;

            if (MyGold < TRAIN_COST_LEVEL_1) break;
        }
    }


    private void AITowerBeforeMove()
    {
        var potentialTargets = MyPositions.Where(p => IsMyBorder(p) && !IsDefended(p)).ToList();

        var potentialTargetsExtended = new List<Position>();
        foreach (var target in potentialTargets)
            potentialTargetsExtended.AddRange(target.Arounds(true).Where(p => !IsDefended(p) && Map[p.X, p.Y].IsOwned).ToList());
        // Make them distinct
        potentialTargets = potentialTargetsExtended.GroupBy(i => (i.X, i.Y)).Select(x => x.First()).ToList();
        potentialTargets.RemoveAll(p => p.Arounds(true).Where(pA => IsMyBorder(pA) && Map[pA.X, pA.Y].IsOccupied && !IsDefended(pA)).ToList().Count < 2);
        potentialTargets.RemoveAll(p => Map[p.X, p.Y].HasMineSpot);

        while (MyGold > BUILD_COST_TOWER && potentialTargets.Count > 0)
        {
            potentialTargets.Sort(delegate (Position p1, Position p2)
            {
                int count1 = p1.Arounds(true).Where(p => IsMyBorder(p) && Map[p.X, p.Y].IsOccupied && !IsDefended(p)).ToList().Count;
                int count2 = p2.Arounds(true).Where(p => IsMyBorder(p) && Map[p.X, p.Y].IsOccupied && !IsDefended(p)).ToList().Count;
                if (count1 != count2)
                    return count2 - count1;
                else
                    return (int)(Math.Abs(11 - (p1.X + p1.Y)) - Math.Abs(11 - (p2.X + p2.Y)));
                // Or closer to our Hq ??
            });

            var target = potentialTargets[0];
            int soldierCount = target.Arounds(true).Where(p => IsMyBorder(p) && Map[p.X, p.Y].IsOccupied && !IsDefended(p)).ToList().Count;
            if (soldierCount < 2)
                break;
            else
            {
                List<Unit> onTheWayUnits = MyUnits.FindAll(u => u.Position.Arounds(true).Exists(p => p == target));
                FreePlace(target);
                Build("TOWER", target);
                potentialTargets.RemoveAt(0);
            }

            //potentialTargets.RemoveAll(p => IsDefended(p));
            potentialTargets.RemoveAll(p => p.Arounds(true).Where(pA => IsMyBorder(pA) && Map[pA.X, pA.Y].IsOccupied && !IsDefended(pA)).ToList().Count < 2);
        }
    }

    private int AIFindBestCut(out List<Position> positionsToCut)
    {
        positionsToCut = new List<Position>();
        int bestBenefit = 0;
        int bestCost = MyGold + 1;
        float bestRatio = 0.1f;

        foreach (var position in MyPositions)
        {
            if (IsMyBorder(position))
            {
                foreach (var around in position.Arounds().Where(p => Map[p.X, p.Y].IsOpponent).ToList())
                {
                    string debugLine = "";

                    int xDiff = around.X - position.X;
                    int yDiff = around.Y - position.Y;
                    List<Position> passBy = new List<Position>();
                    Position nextPosition = around;
                    int cost = 0;
                    while (IsInside(nextPosition) && Map[nextPosition.X, nextPosition.Y].IsOpponent)
                    {
                        passBy.Add((nextPosition.X, nextPosition.Y));

                        int gain = CalculCutBenefit(OpponentHq, passBy);
                        // To calcul the cost
                        // IsDefended(nextPosition) but with potential tower loose.
                        var arounds = nextPosition.Arounds(true);
                        arounds.Remove((nextPosition.X - xDiff, nextPosition.Y - yDiff));
                        arounds.RemoveAll(p => !(Map[p.X, p.Y].OccupiedBy is Building));
                        arounds.RemoveAll(p => !(Map[p.X, p.Y].OccupiedBy as Building).IsTower);
                        if (arounds.Count > 0)
                        {
                            cost += TRAIN_COST_LEVEL_3;
                        }
                        else
                        {
                            Unit opponentUnit = Map[nextPosition.X, nextPosition.Y].OccupiedBy as Unit;
                            if (opponentUnit != null)
                            {
                                if (opponentUnit.Level == 1)
                                    cost += TRAIN_COST_LEVEL_2;
                                else
                                    cost += TRAIN_COST_LEVEL_3;
                            }
                            else
                            {
                                cost += TRAIN_COST_LEVEL_1;
                            }
                        }

                        if (cost > MyGold) break;

                        nextPosition.X += xDiff;
                        nextPosition.Y += yDiff;

                        debugLine += String.Format(" ! Cost : {0}", cost);
                        int benefit = CalculCutBenefit(OpponentHq, passBy);
                        debugLine += String.Format(" ! Benefit : {0}", benefit);
                        if (CalculCutBenefit(OpponentHq, passBy) != benefit)
                        {
                            Console.Error.WriteLine("SHOULD NOT HAPPEN !!! Calcul of Cut benefit change it.");
                        }

                        float ratio = (float)benefit / cost;
                        debugLine += String.Format(" & Ratio {0} - Best {1}", ratio, bestRatio);
                        if (ratio > bestRatio)
                        {
                            bestBenefit = benefit;
                            bestCost = cost;
                            bestRatio = ratio;
                            positionsToCut = passBy.ToList();
                        }

                        // Want to debug
#if SHOW_DEBUG
                                if (ratio > 0.1f)
                                    Console.Error.WriteLine(debugLine);
#endif
                    }

                }
            }
        }

        return bestCost;
    }


    private void AIMovesToExtend()
    {
        var unitsToMove = MyUnits;
        // We remove all the unit in range of opponent. 
        // We want them to hold the position. 
        unitsToMove.RemoveAll(u => u.GetBorders().Exists(p => Map[p.X, p.Y].IsOpponent && Map[p.X, p.Y].Active && !IsDefended(u.Position)));
        unitsToMove.RemoveAll(u => u.IsMoved || u.Level == 3);

        unitsToMove.Reverse();

        SendOneToCollect(ref unitsToMove);
        int baseUnitCount = 0;
        while (unitsToMove.Count != baseUnitCount && unitsToMove.Count != 0)
        {
            baseUnitCount = unitsToMove.Count;
            SendToCloserEmpty(ref unitsToMove);
        }

        MovesUnitLevelThree();
    }

    private void AITrainsToExtend()
    {
        var targetsOnBorder = NeutralPositions;
        targetsOnBorder.AddRange(OpponentPositions.Where(p => !IsDefended(p) && (!Map[p.X, p.Y].IsOccupied)).ToList());
        // Remove all position if no tile owned around. 
        targetsOnBorder.RemoveAll(p => !p.Arounds().Exists(pA => Map[pA.X, pA.Y].IsOwned
                                                        && Map[pA.X, pA.Y].Active));
        // Remove all position if no tile opponent around. 
        targetsOnBorder.RemoveAll(p => !p.Arounds().Exists(pA => Map[pA.X, pA.Y].IsOpponent));

        // We want to cover our half
        targetsOnBorder.RemoveAll(p => p.Dist(MyHq) > 10);
        targetsOnBorder.Sort(delegate (Position p1, Position p2) {
            var distToCenter1 = Math.Abs(p1.X - p1.Y);
            var distToCenter2 = Math.Abs(p2.X - p2.Y);
            return (int)(distToCenter1 - distToCenter2);
        });

        // Defend against level 3.
        foreach (var target in targetsOnBorder)
        {
            if (MyGold < TRAIN_COST_LEVEL_3) break;

            Unit opponentUnit = Map[target.X, target.Y].OccupiedBy as Unit;
            if (opponentUnit != null && opponentUnit.Level == 3)
            {
                if (opponentUnit.GetBorders().Exists(p => Map[p.X, p.Y].IsOwned && IsDefended(p)))
                    Train(3, target);
            }
        }

        while (MyGold >= TRAIN_COST_LEVEL_1 && targetsOnBorder.Count > 0)
        {
            Train(1, targetsOnBorder[0]);

            // Update info as before the while.
            targetsOnBorder = NeutralPositions;
            targetsOnBorder.AddRange(OpponentPositions.Where(p => !IsDefended(p) && (!Map[p.X, p.Y].IsOccupied)).ToList());
            targetsOnBorder.RemoveAll(p => !p.Arounds().Exists(pA => Map[pA.X, pA.Y].IsOwned
                                                            && Map[pA.X, pA.Y].Active));
            targetsOnBorder.RemoveAll(p => !p.Arounds().Exists(pA => Map[pA.X, pA.Y].IsOpponent));

            // We want to cover our half
            targetsOnBorder.RemoveAll(p => p.Dist(MyHq) > 10);
            targetsOnBorder.Sort(delegate (Position p1, Position p2) {
                var distToCenter1 = Math.Abs(p1.X - p1.Y);
                var distToCenter2 = Math.Abs(p2.X - p2.Y);
                return (int)(distToCenter1 - distToCenter2);
            });
        }
        TrainAroundLeader();
    }


    private void AIBlockLevel2()
    {
        // Defense vs level 2
        List<Unit> opponentsToBlock = OpponentUnits.FindAll(u => u.Level == 2);
        while (opponentsToBlock.Count > 0 && MyGold >= BUILD_COST_TOWER)
        {
            Position placeToLock = opponentsToBlock[0].Position;
            var towerOpportunities = new List<Position>();
            Position diagonal;
            // - - 
            diagonal = (placeToLock.X - Offset, placeToLock.Y - Offset);
            towerOpportunities.Add(diagonal);
            // + -  
            diagonal = (placeToLock.X + Offset, placeToLock.Y - Offset);
            towerOpportunities.Add(diagonal);
            // - + 
            diagonal = (placeToLock.X - Offset, placeToLock.Y + Offset);
            towerOpportunities.Add(diagonal);
            // + +
            diagonal = (placeToLock.X + Offset, placeToLock.Y + Offset);
            towerOpportunities.Add(diagonal);

            towerOpportunities.RemoveAll(p => IsDefended(p));

            towerOpportunities.Sort(delegate (Position p1, Position p2) {
                int defend1 = Map[p1.X, p1.Y].IsOwned ? 1 : 0;
                defend1 += p1.Arounds().Where(pA => Map[pA.X, pA.Y].IsOwned && !IsDefended(pA)).ToList().Count;
                int defend2 = Map[p1.X, p1.Y].IsOwned ? 1 : 0;
                defend2 += p1.Arounds().Where(pA => Map[pA.X, pA.Y].IsOwned && !IsDefended(pA)).ToList().Count;

                return defend2 - defend1;
            });

            foreach (var position in towerOpportunities)
                if (Build("TOWER", position))
                    break;

            opponentsToBlock.RemoveAt(0);
        }
    }

    private void AIBuildMine() { }
}