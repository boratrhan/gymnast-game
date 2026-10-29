using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts a new attempt (reloads the scene) when R is pressed after the landing, and toggles slow motion with T
/// for practice. Slow motion uses Time.timeScale: Unity then runs FixedUpdate less often per real second but with
/// the same fixed step, so the simulation gives exactly the same motion, only slower to watch.
/// Time.timeScale survives a scene reload, so slow motion stays on for the next attempt.
/// </summary>
public class RoundFlow : MonoBehaviour
{
    [SerializeField] private GymnastController gymnast;
    [SerializeField] private float slowMotionScale = 0.5f;

    public bool IsSlowMotion => Time.timeScale < 1f;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.tKey.wasPressedThisFrame)
            Time.timeScale = IsSlowMotion ? 1f : slowMotionScale;

        if (gymnast.HasLanded && keyboard.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
