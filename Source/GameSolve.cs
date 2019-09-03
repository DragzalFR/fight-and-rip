public partial class Game
{
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
}