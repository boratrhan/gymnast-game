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

    private string feedbackText = "";
    private Color feedbackColor = Color.white;
    private float feedbackTime = -10f;
    private bool hasLanded;

    private GUIStyle feedbackStyle;
    private GUIStyle counterStyle;
    private GUIStyle hintStyle;
    private GUIStyle scoreStyle;

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

    private void HandleLanded(ScoreResult result)
    {
        hasLanded = true;
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
            scoreStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 26, alignment = TextAnchor.MiddleCenter, richText = true
            };
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
            ScoreResult r = gymnast.Result;
            string text =
                $"<b>{r.LandingLabel}</b>\n" +
                $"D {r.Difficulty:F1}  +  E {r.Execution:F1}\n" +
                $"<size=40><b>{r.Total:F2}</b></size>\n" +
                $"<size=18>Air flips: {r.AirFlips}   ·   Press R to try again</size>";
            float w = 420, h = 220;
            GUI.Box(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h), text, scoreStyle);
        }
    }
}
