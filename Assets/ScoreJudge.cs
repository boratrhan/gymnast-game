using System.Collections.Generic;
using UnityEngine;

/// <summary>One item of the score breakdown, e.g. +1.0 "1 salto" or -0.3 "small step".</summary>
public readonly struct ScoreLine
{
    public readonly float Points;   // positive for difficulty, negative for a deduction
    public readonly string Label;

    public ScoreLine(float points, string label)
    {
        Points = points;
        Label = label;
    }
}

/// <summary>The judges' verdict for one routine.</summary>
public readonly struct ScoreResult
{
    public readonly float Difficulty;   // D score: what the gymnast attempted
    public readonly float Execution;    // E score: how cleanly it was done (starts at 10)
    public readonly int AirFlips;
    public readonly string LandingLabel;

    /// <summary>What added to D, one line per item. The lines add up to Difficulty.</summary>
    public readonly IReadOnlyList<ScoreLine> DifficultyLines;

    /// <summary>Each E deduction on its own line. 10 plus the lines gives Execution (never below 0).</summary>
    public readonly IReadOnlyList<ScoreLine> ExecutionLines;

    public float Total => Difficulty + Execution;

    public ScoreResult(int airFlips, string landingLabel,
        IReadOnlyList<ScoreLine> difficultyLines, IReadOnlyList<ScoreLine> executionLines)
    {
        AirFlips = airFlips;
        LandingLabel = landingLabel;
        DifficultyLines = difficultyLines;
        ExecutionLines = executionLines;

        float difficulty = 0f;
        foreach (var line in difficultyLines) difficulty += line.Points;
        float execution = ScoreJudge.ExecutionStart;
        foreach (var line in executionLines) execution += line.Points;

        Difficulty = difficulty;
        Execution = Mathf.Max(execution, 0f);
    }
}

/// <summary>
/// Scores a routine the way real artistic gymnastics does: Difficulty (D) + Execution (E).
/// Pure C# with no MonoBehaviour, so the rules live in one place and can be unit-tested
/// without running the game.
/// </summary>
public static class ScoreJudge
{
    public const float ExecutionStart = 10f;

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

        var difficulty = new List<ScoreLine>();
        if (giantSwings > 0)
            difficulty.Add(new ScoreLine(giantSwings * GiantSwingValue, Count(giantSwings, "giant swing", "giant swings")));
        if (airFlips > 0)
            difficulty.Add(new ScoreLine(airFlips * AirFlipValue, Count(airFlips, "salto", "saltos")));

        var execution = new List<ScoreLine>();
        if (misses > 0)
        {
            float missDeduction = Mathf.Min(misses * MissDeduction, MaxMissDeduction);
            execution.Add(new ScoreLine(-missDeduction, Count(misses, "missed pump", "missed pumps")));
        }

        string label;
        if (landingErrorDegrees <= StuckTolerance)
        {
            label = "STUCK IT!";
        }
        else if (landingErrorDegrees <= StepTolerance)
        {
            label = "Small step";
            execution.Add(new ScoreLine(-SmallStepDeduction, "small step"));
        }
        else
        {
            label = "Fall";
            execution.Add(new ScoreLine(-FallDeduction, "fall"));
        }

        return new ScoreResult(airFlips, label, difficulty, execution);
    }

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
}
