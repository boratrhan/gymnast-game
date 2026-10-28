using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Simple on-screen feedback: PERFECT / GOOD / OK / MISS after each tap, a giant swing counter,
/// the controls, and the judges' score after landing. Uses Unity's immediate-mode GUI (OnGUI),
/// which needs no extra packages and is fine for a prototype; we can swap it for a proper UI later.
/// Attach to any object and drag the Bar (with GymnastController) into the "Gymnast" field.
/// </summary>
public class SwingHUD : MonoBehaviour
{
    [SerializeField] private GymnastController gymnast;
    [SerializeField] private float feedbackDuration = 0.8f;
    [SerializeField] private Color cardColor = new Color(0.07f, 0.08f, 0.11f, 0.94f);

    private string feedbackText = "";
    private Color feedbackColor = Color.white;
    private float feedbackTime = -10f;
    private bool hasLanded;

    private GUIStyle feedbackStyle;
    private GUIStyle counterStyle;
    private GUIStyle hintStyle;
    private GUIStyle scoreStyle;
    private Texture2D cardBackground;
    private string scoreText;
    private int scoreLineCount;

    // Subscribe in OnEnable / unsubscribe in OnDisable so we never leak event handlers.
    private void OnEnable()
    {
        if (gymnast == null) return;
        gymnast.Pumped += HandlePumped;
        gymnast.FullTurnCompleted += HandleFullTurn;
        gymnast.Released += HandleReleased;
        gymnast.Landed += HandleLanded;
    }

    private void OnDisable()
    {
        if (gymnast == null) return;
        gymnast.Pumped -= HandlePumped;
        gymnast.FullTurnCompleted -= HandleFullTurn;
        gymnast.Released -= HandleReleased;
        gymnast.Landed -= HandleLanded;
    }

    private void HandlePumped(float quality)
    {
        if (quality >= 0.8f)      ShowFeedback("PERFECT!", new Color(1f, 0.85f, 0.2f));
        else if (quality >= 0.5f) ShowFeedback("GOOD", new Color(0.4f, 0.9f, 0.4f));
        else if (quality > 0f)    ShowFeedback("OK", Color.white);
        else                      ShowFeedback("MISS", new Color(1f, 0.4f, 0.4f));
    }

    private void HandleFullTurn(int turns) => ShowFeedback("GIANT SWING!", new Color(0.5f, 0.8f, 1f));

    private void HandleReleased() => ShowFeedback("RELEASE!", Color.white);

    private void OnDestroy()
    {
        if (cardBackground != null) Destroy(cardBackground);
    }

    private void HandleLanded(ScoreResult result)
    {
        hasLanded = true;
        BuildScoreText(result);
        Color c = result.LandingLabel == "Fall" ? new Color(1f, 0.4f, 0.4f) : new Color(1f, 0.85f, 0.2f);
        ShowFeedback(result.LandingLabel, c);
    }

    private void ShowFeedback(string text, Color color)
    {
        feedbackText = text;
        feedbackColor = color;
        feedbackTime = Time.time;
    }

    private void OnGUI()
    {
        // Styles can only be created inside OnGUI, so build them lazily once.
        if (feedbackStyle == null)
        {
            feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            counterStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            // The default box skin is see-through; a plain dark texture keeps the text readable.
            cardBackground = new Texture2D(1, 1);
            cardBackground.SetPixel(0, 0, cardColor);
            cardBackground.Apply();
            scoreStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 24, alignment = TextAnchor.MiddleCenter, richText = true,
                padding = new RectOffset(20, 20, 16, 16)
            };
            scoreStyle.normal.background = cardBackground;
            scoreStyle.normal.textColor = Color.white;
        }

        if (gymnast == null) return;

        GUI.Label(new Rect(20, 20, 400, 40), $"Giant swings: {gymnast.FullTurns}", counterStyle);

        float age = Time.time - feedbackTime;
        if (age < feedbackDuration)
        {
            // Fade out over the feedback duration.
            Color c = feedbackColor;
            c.a = 1f - age / feedbackDuration;
            feedbackStyle.normal.textColor = c;
            GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, 80), feedbackText, feedbackStyle);
        }

        if (gymnast.IsSwinging)
        {
            GUI.Label(new Rect(0, Screen.height - 50, Screen.width, 30),
                "SPACE / click: pump at the bottom     ENTER / right-click: release", hintStyle);
        }

        if (hasLanded)
        {
            float w = 440, h = 230 + scoreLineCount * 28;
            GUI.Box(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h), scoreText, scoreStyle);
        }
    }

    // Built once on landing, not every OnGUI call.
    private void BuildScoreText(ScoreResult result)
    {
        var text = new StringBuilder();
        text.Append($"<b>{result.LandingLabel}</b>\n\n");
        text.Append($"<b>D  {result.Difficulty:F1}</b>\n");
        AppendLines(text, result.DifficultyLines, "no difficulty yet");
        text.Append($"\n<b>E  {result.Execution:F1}</b>\n");
        AppendLines(text, result.ExecutionLines, "no deductions");
        text.Append($"\n<size=40><b>{result.Total:F2}</b></size>\n");
        text.Append("<size=18>Press R to try again</size>");

        scoreText = text.ToString();
        scoreLineCount = Mathf.Max(result.DifficultyLines.Count, 1) + Mathf.Max(result.ExecutionLines.Count, 1);
    }

    private static void AppendLines(StringBuilder text, IReadOnlyList<ScoreLine> lines, string emptyText)
    {
        if (lines.Count == 0)
        {
            text.Append($"<size=18><color=#9AA0A6>{emptyText}</color></size>\n");
            return;
        }
        foreach (var line in lines)
        {
            string colour = line.Points >= 0f ? "#8FE38F" : "#FF8A80";
            text.Append($"<size=20><color={colour}>{line.Points:+0.0;-0.0}</color>   {line.Label}</size>\n");
        }
    }
}
