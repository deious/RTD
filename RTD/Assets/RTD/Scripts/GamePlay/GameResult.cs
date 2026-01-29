using UnityEngine;

public enum GameEndType
{
    Win,
    Lose
}

public struct GameResult
{
    public GameEndType endType;
    public int reachedWave;

    public GameResult(GameEndType endType, int reachedWave)
    {
        this.endType = endType;
        this.reachedWave = reachedWave;
    }
}

