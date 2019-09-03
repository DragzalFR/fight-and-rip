//#define SHOW_DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ACOIF
{
    public class ACodeOfIceAndFire
    {
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

        public class Game
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

            /***
             * -----------------------------------------------------------
             * The core of the AI
             * -----------------------------------------------------------
             */

            public void Solve()
            {
                if (TryToWin()) return;

                if (Turn == 0)
                    SetUpWithSimulation();

                // Mid and Late game
                List<Position> positionsToCut;
                int costToCut;
                int gainToCut;
                int goldBeforeCut;

                string debugText = "";

                switch (Stage)
                {
                    case GameStage.Early:
                        SetOutputFromSimulation();
                        if (Output.Length == 0)
                        {
                            AIMovesToScout();
                            AITrainsToScout();
                        }
                        break;
                    case GameStage.Mid:
                        AITowerBeforeMove();
                        AIBlockLevel2();
                        AIMovesToExtend();

                        do
                        {
                            goldBeforeCut = MyGold;
                            costToCut = AIFindBestCut(out positionsToCut);
                            gainToCut = CalculCutBenefit(OpponentHq, positionsToCut);
                            if (gainToCut > costToCut)
                                ApplyCut(in positionsToCut);
                            else
                                break;
                        } while (positionsToCut.Count > 0 && MyGold != goldBeforeCut);
                        AITrainsToExtend();
                        break;
                    case GameStage.Late:
                        do
                        {
                            goldBeforeCut = MyGold;
                            costToCut = AIFindBestCut(out positionsToCut);
                            gainToCut = CalculCutBenefit(OpponentHq, positionsToCut);
                            if (gainToCut > costToCut)
                                ApplyCut(in positionsToCut);
                            else
                                break;
                        } while (positionsToCut.Count > 0 && MyGold != goldBeforeCut);
                        AIMovesToExtend();
                        do
                        {
                            goldBeforeCut = MyGold;
                            costToCut = AIFindBestCut(out positionsToCut);
                            gainToCut = CalculCutBenefit(OpponentHq, positionsToCut);
                            if (gainToCut >= costToCut)
                                ApplyCut(in positionsToCut);
                            else
                                break;
                        } while (positionsToCut.Count > 0 && MyGold != goldBeforeCut);
                        break;
                    default: break;
                }

                Turn++;

                // Make sur the AI doesn't timeout
                Wait();

                Output.Append("MSG Danger level : " + (int)Stage + ";");
            }

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
                        if(opponentUnit.GetBorders().Exists(p => Map[p.X, p.Y].IsOwned && IsDefended(p)))
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

            // TODO: Handle Build command for Mine
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

        public class Unit : Entity
        {
            public int Id;
            public int Level;

            public bool IsMoved = false;

            public override string ToString() => $"Unit => {base.ToString()} Id: {Id} Level: {Level}";

            public static bool operator ==(Unit obj1, Unit obj2)
            {
                if (object.ReferenceEquals(null, obj1))
                    return object.ReferenceEquals(null, obj2);
                return obj1.Equals(obj2);
            }

            public static bool operator !=(Unit obj1, Unit obj2)
            {
                if (object.ReferenceEquals(null, obj1))
                    return !object.ReferenceEquals(null, obj2);
                return !obj1.Equals(obj2);
            }

            public override bool Equals(object obj) => Equals((Unit)obj);

            protected bool Equals(Unit other)
            {
                if (object.ReferenceEquals(other, null))
                    return false;
                return (Id == other.Id);
            }

            private List<Position> borderPositions;
            private int borderRange = 0;

            public List<Position> GetBorders(bool increment = false)
            {
                if (!increment)
                {
                    borderRange = 0;
                    return Position.Arounds();
                }
                else
                {
                    if (borderRange == 0)
                        borderPositions = Position.Arounds();
                    else
                    {
                        var nextBorderPositions = new List<Position>();
                        foreach (var border in borderPositions)
                            nextBorderPositions.AddRange(border.Arounds());
                        nextBorderPositions.RemoveAll(p => p.Dist(Position) <= borderRange);

                        borderPositions = nextBorderPositions;
                    }

                    borderRange++;
                    return borderPositions;
                }
            }
        }

        public class Building : Entity
        {
            public BuildingType Type;

            public bool IsHq => Type == BuildingType.Hq;
            public bool IsTower => Type == BuildingType.Tower;
            public bool IsMine => Type == BuildingType.Mine;

            public override string ToString() => $"Building => {base.ToString()} Type: {Type}";
        }

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

        public class Position
        {
            public int X;
            public int Y;

            public static implicit operator Position(ValueTuple<int, int> cell) => new Position
            {
                X = cell.Item1,
                Y = cell.Item2
            };

            public override string ToString() => $"({X},{Y})";

            public static bool operator ==(Position obj1, Position obj2)
            {
                if (object.ReferenceEquals(null, obj1))
                    return object.ReferenceEquals(null, obj2);
                return obj1.Equals(obj2);
            }

            public static bool operator !=(Position obj1, Position obj2)
            {
                if (object.ReferenceEquals(null, obj1))
                    return !object.ReferenceEquals(null, obj2);
                return !obj1.Equals(obj2);
            }

            public override bool Equals(object obj) => Equals((Position)obj);

            protected bool Equals(Position other)
            {
                if (object.ReferenceEquals(other, null))
                    return false;
                return (X == other.X && Y == other.Y);
            }

            public double Dist(Position p) => Math.Abs(X - p.X) + Math.Abs(Y - p.Y);

            public List<Position> Arounds(bool includeSelf = false)
            {
                var result = new List<Position>();

                Position up = (X, Y - 1);
                Position down = (X, Y + 1);
                Position left = (X - 1, Y);
                Position right = (X + 1, Y);

                if (IsInside(up)) result.Add(up);
                if (IsInside(down)) result.Add(down);
                if (IsInside(left)) result.Add(left);
                if (IsInside(right)) result.Add(right);
                if (includeSelf) result.Add((X, Y));

                return result;
            }
        }
    }
}