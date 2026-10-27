using UnityEngine;

/// <summary>
/// Simple on-screen feedback for the swing: shows PERFECT / GOOD / OK / MISS after each tap
/// and counts full turns over the bar. Uses Unity's immediate-mode GUI (OnGUI), which needs
/// no extra packages and is fine for a prototype; we can swap it for a proper UI later.
/// Attach to any object and drag the Bar (with GymnastSwing) into the "Swing" field.
/// </summary>
public class SwingHUD : MonoBehaviour
{
    [SerializeField] private GymnastSwing swing;
    [SerializeField] private float feedbackDuration = 0.8f;

    private string feedbackText = "";
    private Color feedbackColor = Color.white;
    private float feedbackTime = -10f;

    private GUIStyle feedbackStyle;
    private GUIStyle counterStyle;

    // Subscribe in OnEnable / unsubscribe in OnDisable so we never leak event handlers.
    private void OnEnable()
    {
        if (swing == null) return;
        swing.Pumped += HandlePumped;
        swing.FullTurnCompleted += HandleFullTurn;
    }

    private void OnDisable()
    {
        if (swing == null) return;
        swing.Pumped -= HandlePumped;
        swing.FullTurnCompleted -= HandleFullTurn;
    }

    private void HandlePumped(float quality)
    {
        if (quality >= 0.8f)      ShowFeedback("PERFECT!", new Color(1f, 0.85f, 0.2f));
        else if (quality >= 0.5f) ShowFeedback("GOOD", new Color(0.4f, 0.9f, 0.4f));
        else if (quality > 0f)    ShowFeedback("OK", Color.white);
        else                      ShowFeedback("MISS", new Color(1f, 0.4f, 0.4f));
    }

    private void HandleFullTurn(int turns)
    {
        ShowFeedback("GIANT SWING!", new Color(0.5f, 0.8f, 1f));
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
        }

        if (swing != null)
            GUI.Label(new Rect(20, 20, 400, 40), $"Giant swings: {swing.FullTurns}", counterStyle);

        float age = Time.time - feedbackTime;
        if (age < feedbackDuration)
        {
            // Fade out over the feedback duration.
            Color c = feedbackColor;
            c.a = 1f - age / feedbackDuration;
            feedbackStyle.normal.textColor = c;
            GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, 80), feedbackText, feedbackStyle);
        }
    }
}
