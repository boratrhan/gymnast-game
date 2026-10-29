using System.Collections.Generic;
using GymnastGame.Simulation;
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
    private GUIStyle cardStyle;
    private GUIStyle cardTitleStyle;
    private GUIStyle cardHeaderStyle;
    private GUIStyle cardLineStyle;
    private GUIStyle cardTotalStyle;
    private GUIStyle cardHintStyle;
    private Texture2D cardBackground;

    // Score card layout, in unscaled pixels. The card shrinks to fit small windows.
    private const float CardWidth = 440f;
    private const float CardPadding = 20f;
    private const float CardGap = 10f;
    private const float ScreenMargin = 16f;

    private enum CardRowKind { Title, Header, Line, Total, Hint, Gap }

    private readonly List<CardRowKind> cardRowKinds = new List<CardRowKind>();
    private readonly List<GUIContent> cardRowContents = new List<GUIContent>();
    private readonly List<float> cardRowHeights = new List<float>();     // measured once, on the first draw

    // Subscribe in OnEnable / unsubscribe in OnDisable so we never leak event handlers.
    private void OnEnable()
    {
        if (gymnast == null) return;
        gymnast.Pumped += HandlePumped;
        gymnast.FullTurnCompleted += HandleFullTurn;
        gymnast.Released += HandleReleased;
        gymnast.Regrasped += HandleRegrasped;
        gymnast.CatchSlipped += HandleCatchSlipped;
        gymnast.CatchFailed += HandleCatchFailed;
        gymnast.Landed += HandleLanded;
    }

    private void OnDisable()
    {
        if (gymnast == null) return;
        gymnast.Pumped -= HandlePumped;
        gymnast.FullTurnCompleted -= HandleFullTurn;
        gymnast.Released -= HandleReleased;
        gymnast.Regrasped -= HandleRegrasped;
        gymnast.CatchSlipped -= HandleCatchSlipped;
        gymnast.CatchFailed -= HandleCatchFailed;
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
    private void HandleRegrasped(FlightRecord flight) => ShowFeedback("CAUGHT!", new Color(0.4f, 0.9f, 0.4f));
    private void HandleCatchSlipped() => ShowFeedback("SLIPPED! too fast", new Color(1f, 0.4f, 0.4f));
    private void HandleCatchFailed(CatchBlocker reason) => ShowFeedback(CatchFailureText(reason), new Color(1f, 0.7f, 0.15f));

    private static string CatchFailureText(CatchBlocker reason)
    {
        switch (reason)
        {
            case CatchBlocker.TooEarly: return "too early";
            case CatchBlocker.TooFast: return "too fast";
            default: return "too far";
        }
    }

    private void OnDestroy()
    {
        if (cardBackground != null) Destroy(cardBackground);
    }

    private void HandleLanded(ScoreResult result)
    {
        hasLanded = true;
        BuildScoreCard(result);
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
            cardStyle = new GUIStyle(GUI.skin.box);
            cardStyle.normal.background = cardBackground;
            cardTitleStyle = CardTextStyle(30, FontStyle.Bold);
            cardHeaderStyle = CardTextStyle(24, FontStyle.Bold);
            cardLineStyle = CardTextStyle(20, FontStyle.Normal);
            cardTotalStyle = CardTextStyle(40, FontStyle.Bold);
            cardHintStyle = CardTextStyle(18, FontStyle.Normal);
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

        if (gymnast.IsSwinging || gymnast.IsFlying)
        {
            string hint = gymnast.IsSwinging
                ? "SPACE / click: pump at the bottom     ENTER / right-click: release     T: slow motion"
                : "hold A tuck, S pike, D twist     ENTER when the bar lights up: green = clean catch, yellow = catch with deductions";
            GUI.Label(new Rect(0, Screen.height - 50, Screen.width, 30), hint, hintStyle);
        }

        if (Time.timeScale < 1f)
            GUI.Label(new Rect(Screen.width - 260, 20, 240, 40), $"SLOW MOTION {Time.timeScale:0.#}x", counterStyle);

        if (hasLanded)
            DrawScoreCard();
    }

    private void DrawScoreCard()
    {
        float innerWidth = CardWidth - 2f * CardPadding;

        // Measure each row with its real style once; CalcHeight needs the styles, so it runs here.
        if (cardRowHeights.Count != cardRowKinds.Count)
        {
            cardRowHeights.Clear();
            for (int i = 0; i < cardRowKinds.Count; i++)
            {
                cardRowHeights.Add(cardRowKinds[i] == CardRowKind.Gap
                    ? CardGap
                    : StyleFor(cardRowKinds[i]).CalcHeight(cardRowContents[i], innerWidth));
            }
        }

        float height = 2f * CardPadding;
        foreach (float rowHeight in cardRowHeights) height += rowHeight;

        // Shrink the whole card (never enlarge it) so it fits a small Game window.
        float scale = Mathf.Min(1f,
            (Screen.width - 2f * ScreenMargin) / CardWidth,
            (Screen.height - 2f * ScreenMargin) / height);
        var centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), centre);

        var card = new Rect(centre.x - CardWidth / 2f, centre.y - height / 2f, CardWidth, height);
        GUI.Box(card, GUIContent.none, cardStyle);

        float y = card.y + CardPadding;
        for (int i = 0; i < cardRowKinds.Count; i++)
        {
            if (cardRowKinds[i] != CardRowKind.Gap)
                GUI.Label(new Rect(card.x + CardPadding, y, innerWidth, cardRowHeights[i]), cardRowContents[i], StyleFor(cardRowKinds[i]));
            y += cardRowHeights[i];
        }

        GUI.matrix = previousMatrix;
    }

    // Built once on landing, not every OnGUI call.
    private void BuildScoreCard(ScoreResult result)
    {
        cardRowKinds.Clear();
        cardRowContents.Clear();
        cardRowHeights.Clear();

        AddRow(CardRowKind.Title, result.LandingLabel);
        AddRow(CardRowKind.Gap, "");
        AddRow(CardRowKind.Header, $"D  {result.Difficulty:F1}");
        AddScoreLines(result.DifficultyLines, "no difficulty yet");
        AddRow(CardRowKind.Gap, "");
        AddRow(CardRowKind.Header, $"E  {result.Execution:F1}");
        AddScoreLines(result.ExecutionLines, "no deductions");
        AddFlightLines(gymnast.Flights);
        AddRow(CardRowKind.Gap, "");
        AddRow(CardRowKind.Total, $"{result.Total:F2}");
        AddRow(CardRowKind.Hint, "Press R to try again");
    }

    private void AddScoreLines(IReadOnlyList<ScoreLine> lines, string emptyText)
    {
        if (lines.Count == 0)
        {
            AddRow(CardRowKind.Line, $"<color=#9AA0A6>{emptyText}</color>");
            return;
        }
        foreach (var line in lines)
        {
            string colour = line.Points >= 0f ? "#8FE38F" : "#FF8A80";
            AddRow(CardRowKind.Line, $"<color={colour}>{line.Points:+0.0;-0.0}</color>   {line.Label}");
        }
    }

    // One line per flight, for example "2  2 saltos · tuck · over the bar · caught".
    private void AddFlightLines(IReadOnlyList<FlightRecord> flights)
    {
        if (flights.Count == 0) return;
        AddRow(CardRowKind.Gap, "");
        AddRow(CardRowKind.Header, "Flights");
        for (int i = 0; i < flights.Count; i++)
        {
            var flight = flights[i];
            var parts = new List<string> { Count(flight.Saltos, "salto", "saltos") };
            if (flight.HalfTwists > 0) parts.Add(Count(flight.HalfTwists, "half twist", "half twists"));
            parts.Add(flight.MainShape.ToString().ToLowerInvariant());
            if (flight.OverBar) parts.Add("over the bar");
            parts.Add(flight.End == FlightEnd.Caught ? "caught" : flight.Slipped ? "slipped" : "dismount");
            AddRow(CardRowKind.Line, $"<color=#9AA0A6>{i + 1}</color>   {string.Join(" · ", parts)}");
        }
    }

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    private void AddRow(CardRowKind kind, string text)
    {
        cardRowKinds.Add(kind);
        cardRowContents.Add(new GUIContent(text));
    }

    private GUIStyle StyleFor(CardRowKind kind)
    {
        switch (kind)
        {
            case CardRowKind.Title: return cardTitleStyle;
            case CardRowKind.Header: return cardHeaderStyle;
            case CardRowKind.Total: return cardTotalStyle;
            case CardRowKind.Hint: return cardHintStyle;
            default: return cardLineStyle;
        }
    }

    private static GUIStyle CardTextStyle(int fontSize, FontStyle fontStyle)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize, fontStyle = fontStyle, alignment = TextAnchor.MiddleCenter,
            richText = true, wordWrap = true
        };
        style.normal.textColor = Color.white;
        return style;
    }
}
