public partial class Game
{
    // Simulation for the early ===========================================================

    public void SetUpWithSimulation()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        // the code that you want to measure comes here

        List<Game> simulations = new List<Game>();
        simulations.Add(new Game
        {
            MapInput = this.MapInput.Clone() as char[,],
            MyGold = this.MyGold,
            MyTeam = this.MyTeam,
            Turn = this.Turn,
            OutputsByTurn = new List<string>(),
            // Units = new List<Unit>(),
        });
        simulations[0].OutputsByTurn.Add("WAIT;");

        Game result = simulations[0];

        while (simulations[0].Turn < 8)
        {
            result = simulations[0];

            SimuleMovesAll(ref simulations);

            if (simulations.Count == 0)
                break;
            simulations.Sort(delegate (Game g1, Game g2) {
                double dist1 = 0;
                foreach (var unit in g1.MyUnits)
                    dist1 = Math.Max(dist1, unit.Position.Dist(MyHq));

                double dist2 = 0;
                foreach (var unit in g2.MyUnits)
                    dist2 = Math.Max(dist2, unit.Position.Dist(MyHq));
                return (int)(dist2 - dist1);
            });
            double distMax = 0;
            foreach (var unit in simulations[0].MyUnits)
                distMax = Math.Max(distMax, unit.Position.Dist(MyHq));
            for (var i = 1; i < simulations.Count; i++)
            {
                double dist = 0;
                foreach (var unit in simulations[i].MyUnits)
                    dist = Math.Max(dist, unit.Position.Dist(MyHq));

                if (dist < distMax)
                    simulations.RemoveRange(i, simulations.Count - i);
            }

            SimuleTrainsAll(ref simulations);
            if (simulations.Count == 0)
                break;

            SimuleUpkeepAll(ref simulations);

            simulations.Sort(delegate (Game g1, Game g2) {
                return g2.MyIncome - g1.MyIncome;
            });
            simulations.RemoveAll(s => s.MyIncome < simulations[0].MyIncome);

            // Remove when to many empty
            int EMPTY_RANGE_MIN = 6 - (simulations.Count / 1000);
            int EMPTY_RANGE_MAX = 11;
            for (int EMPTY_RANGE = EMPTY_RANGE_MIN; EMPTY_RANGE < EMPTY_RANGE_MAX; EMPTY_RANGE++)
            {
                simulations.Sort(delegate (Game g1, Game g2) {
                    int empty1 = 0;
                    int empty2 = 0;
                    for (var y = 0; y < EMPTY_RANGE; y++)
                        for (var x = 0; x < EMPTY_RANGE; x++)
                        {
                            if (g1.MapInput[x, y] == '.') empty1++;
                            if (g2.MapInput[x, y] == '.') empty2++;
                        }
                    return empty1 - empty2;
                });
                int emptyMin = 0;
                for (var y = 0; y < EMPTY_RANGE; y++)
                    for (var x = 0; x < EMPTY_RANGE; x++)
                        if (simulations[0].MapInput[x, y] == '.') emptyMin++;
                for (var i = 1; i < simulations.Count; i++)
                {
                    int empty = 0;
                    for (var y = 0; y < EMPTY_RANGE; y++)
                        for (var x = 0; x < EMPTY_RANGE; x++)
                            if (simulations[i].MapInput[x, y] == '.') empty++;

                    if (empty > emptyMin)
                        simulations.RemoveRange(i, simulations.Count - i);
                }
            }
        }

        if (simulations.Count != 0)
            for (int EMPTY_RANGE = 11 - simulations[0].Turn; EMPTY_RANGE < 11; EMPTY_RANGE++)
            {
                simulations.Sort(delegate (Game g1, Game g2) {
                    int empty1 = 0;
                    int empty2 = 0;
                    for (var y = 0; y < EMPTY_RANGE; y++)
                        for (var x = 0; x < EMPTY_RANGE; x++)
                        {
                            if (g1.MapInput[x, y] == '.') empty1++;
                            if (g2.MapInput[x, y] == '.') empty2++;
                        }
                    return empty1 - empty2;
                });
                int emptyMin = 0;
                for (var y = 0; y < EMPTY_RANGE; y++)
                    for (var x = 0; x < EMPTY_RANGE; x++)
                        if (simulations[0].MapInput[x, y] == '.') emptyMin++;
                for (var i = 1; i < simulations.Count; i++)
                {
                    int empty = 0;
                    for (var y = 0; y < EMPTY_RANGE; y++)
                        for (var x = 0; x < EMPTY_RANGE; x++)
                            if (simulations[i].MapInput[x, y] == '.') empty++;

                    if (empty > emptyMin)
                        simulations.RemoveRange(i, simulations.Count - i);
                }
            }

        watch.Stop();
        var elapsedMs = watch.ElapsedMilliseconds;
        Console.Error.WriteLine("-------------------");
        Console.Error.WriteLine("Real time : " + elapsedMs);

        if (simulations.Count > 0)
        {
            Random random = new Random();
            //result = simulations[random.Next(0, simulations.Count)];
            result = simulations[simulations.Count - 1];
        }

        OutputsByTurn = result.OutputsByTurn;
        OutputsByTurn.RemoveAt(OutputsByTurn.Count - 1);
    }

    private void SimuleMovesAll(ref List<Game> simulations)
    {
        List<Game> result = new List<Game>();
        foreach (var simulation in simulations)
        {
            List<Game> partialResults = new List<Game>();
            partialResults.Add(simulation);

            foreach (var myUnit in simulation.MyUnits)
            {
                List<Game> nextPartialResults = new List<Game>();

                List<Position> destinations = new List<Position>();
                destinations.Add((myUnit.X - 1, myUnit.Y));
                destinations.Add((myUnit.X + 1, myUnit.Y));
                destinations.Add((myUnit.X, myUnit.Y - 1));
                destinations.Add((myUnit.X, myUnit.Y + 1));
                destinations.RemoveAll(p => !IsInside(p) || p == MyHq);

                foreach (var destination in destinations)
                {
                    // For each simulation present when we start to treat this unit. 
                    foreach (var intermediateResult in partialResults)
                    {
                        // If the destination is valid in this simulation. 
                        if (intermediateResult.MapInput[destination.X, destination.Y] == '.')
                        {
                            Game game = new Game
                            {
                                MapInput = intermediateResult.MapInput.Clone() as char[,],
                                MyGold = intermediateResult.MyGold,
                                MyTeam = intermediateResult.MyTeam,
                                Turn = intermediateResult.Turn,
                                OutputsByTurn = intermediateResult.OutputsByTurn.ToList(),
                                Units = intermediateResult.Units.ToList(),
                            };
                            game.OutputsByTurn[game.Turn] += String.Format("MOVE {0} {1} {2};", myUnit.Id, destination.X, destination.Y);

                            game.MapInput[myUnit.X, myUnit.Y] = 'O';
                            game.MapInput[destination.X, destination.Y] = 'u';
                            game.Units[myUnit.Id - 1] = new Unit
                            {
                                Id = myUnit.Id,
                                Position = destination,
                                Owner = ME
                            };

                            nextPartialResults.Add(game);
                        }
                    }
                }

                partialResults = nextPartialResults;
            }

            result.AddRange(partialResults);
        }

        simulations.Clear();
        simulations = result;
    }

    private void SimuleTrainsAll(ref List<Game> simulations)
    {
        List<Game> result = new List<Game>();
        foreach (var simulation in simulations)
        {
            List<Game> partialResults = new List<Game>();
            partialResults.Add(simulation);

            while (simulation.MyGold >= TRAIN_COST_LEVEL_1)
            {
                simulation.MyGold -= TRAIN_COST_LEVEL_1;

                List<Game> nextPartialResults = new List<Game>();

                foreach (var intermediateResult in partialResults)
                {
                    var scouts = intermediateResult.MyUnits;
                    if (MyTeam == Team.Fire)
                        scouts.Add(new Unit { Id = -1, Position = (0, 0), Owner = ME });
                    if (MyTeam == Team.Ice)
                    {
                        scouts.Add(new Unit { Id = -1, Position = (11, 10), Owner = ME });
                    }

                    scouts.Sort(delegate (Unit u1, Unit u2) {
                        var dist1 = u1.Position.Dist(OpponentHq);
                        var dist2 = u2.Position.Dist(OpponentHq);
                        return (int)(dist1 - dist2);
                    });
                    scouts.RemoveAll(u => u.Position.Dist(OpponentHq) > scouts[0].Position.Dist(OpponentHq));

                    foreach (var scout in scouts)
                    {
                        List<Position> destinations = new List<Position>();
                        destinations.Add((scout.X + Offset, scout.Y));
                        destinations.Add((scout.X, scout.Y + Offset));
                        destinations.RemoveAll(p => !IsInside(p));

                        foreach (var destination in destinations)
                        {
                            // If the destination is valid in this simulation. 
                            if (intermediateResult.MapInput[destination.X, destination.Y] == '.')
                            {
                                Game game = new Game
                                {
                                    MapInput = intermediateResult.MapInput.Clone() as char[,],
                                    MyGold = simulation.MyGold,
                                    MyTeam = simulation.MyTeam,
                                    Turn = simulation.Turn,
                                    OutputsByTurn = intermediateResult.OutputsByTurn.ToList(),
                                    Units = intermediateResult.Units.ToList(),
                                };
                                game.OutputsByTurn[game.Turn] += String.Format("TRAIN 1 {0} {1};", destination.X, destination.Y);

                                game.MapInput[destination.X, destination.Y] = 'u';
                                game.Units.Add(new Unit
                                {
                                    Id = game.Units.Count + 1,
                                    Position = destination,
                                    Owner = ME
                                });
                                nextPartialResults.Add(game);
                            }
                        }
                    }
                }

                partialResults = nextPartialResults;
            }

            result.AddRange(partialResults);
        }

        simulations.Clear();
        simulations = result;
    }

    private void SimuleUpkeepAll(ref List<Game> simulations)
    {
        foreach (var simulation in simulations)
        {
            simulation.MyIncome = 0;
            foreach (char tile in simulation.MapInput)
                if (tile == 'O')
                    simulation.MyIncome++;

            foreach (var myUnit in simulation.MyUnits)
                simulation.MapInput[myUnit.X, myUnit.Y] = 'U';

            simulation.MyGold += simulation.MyIncome;

            simulation.Turn++;
            simulation.OutputsByTurn.Add("WAIT;");
        }
    }

    public void SetOutputFromSimulation()
    {
        Output.Clear();

        if (Turn <= OutputsByTurn.Count - 1)
        {
            string[] instructions = OutputsByTurn[Turn].Split(";");
            OutputsByTurn[Turn] = "";
            for (var i = 0; i < instructions.Length - 1; i++)
            {
                if (!instructions[i].Contains("MOVE"))
                {
                    OutputsByTurn[Turn] += instructions[i] + ";";
                    continue;
                }

                string nextInstruction = "";
                for (var j = 0; j < MyUnits.Count; j++)
                {
                    if (j + 1 == MyUnits[j].Id)
                        continue;

                    string simu = String.Format("MOVE {0} ", j + 1);
                    string real = String.Format("MOVE {0} ", MyUnits[j].Id);
                    string replace = instructions[i].Replace(simu, real);

                    if (replace != instructions[i])
                    {
                        nextInstruction = replace;
                        break;
                    }
                }

                if (nextInstruction == "")
                    nextInstruction = instructions[i];

                OutputsByTurn[Turn] += nextInstruction + ";";
            }

            Output.Append(OutputsByTurn[Turn]);
        }
        else
            Stage = GameStage.Mid;
    }
}