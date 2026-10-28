using UnityEngine;

/// <summary>The judges' verdict for one routine.</summary>
public readonly struct ScoreResult
{
    public readonly float Difficulty;   // D score: what the gymnast attempted
    public readonly float Execution;    // E score: how cleanly it was done (starts at 10)
    public readonly int AirFlips;
    public readonly string LandingLabel;

    public float Total => Difficulty + Execution;

    public ScoreResult(float difficulty, float execution, int airFlips, string landingLabel)
    {
        Difficulty = difficulty;
        Execution = execution;
        AirFlips = airFlips;
        LandingLabel = landingLabel;
    }
}

/// <summary>
/// Scores a routine the way real artistic gymnastics does: Difficulty (D) + Execution (E).
/// Pure C# with no MonoBehaviour, so the rules live in one place and can be unit-tested
/// without running the game.
/// </summary>
public static class ScoreJudge
{
    // Difficulty values
    public const float GiantSwingValue = 0.5f;
    public const float AirFlipValue = 1.0f;

    // Execution deductions (from a starting 10.0)
    public const float MissDeduction = 0.1f;
    public const float MaxMissDeduction = 1.0f;
    public const float SmallStepDeduction = 0.3f;
    public const float FallDeduction = 1.0f;

    // Landing tolerances, in degrees away from perfectly upright
    public const float StuckTolerance = 15f;
    public const float StepTolerance = 45f;

    public static bool IsFall(float landingErrorDegrees) => landingErrorDegrees > StepTolerance;

    public static ScoreResult Evaluate(int giantSwings, float airRotationDegrees, float landingErrorDegrees, int misses)
    {
        int airFlips = Mathf.FloorToInt(Mathf.Abs(airRotationDegrees) / 360f);
        float difficulty = giantSwings * GiantSwingValue + airFlips * AirFlipValue;

        float execution = 10f - Mathf.Min(misses * MissDeduction, MaxMissDeduction);

        string label;
        if (landingErrorDegrees <= StuckTolerance)
        {
            label = "STUCK IT!";
        }
        else if (landingErrorDegrees <= StepTolerance)
        {
            label = "Small step";
            execution -= SmallStepDeduction;
        }
        else
        {
            label = "Fall";
            execution -= FallDeduction;
        }

        return new ScoreResult(difficulty, Mathf.Max(execution, 0f), airFlips, label);
    }
}
