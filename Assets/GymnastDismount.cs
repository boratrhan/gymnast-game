using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles letting go of the bar: the gymnast flies off with the speed they had on the swing,
/// keeps spinning, and is judged on the landing. Put this on the Bar next to GymnastSwing.
/// Controls: Enter or right-click = release, R = try again after landing.
/// </summary>
public class GymnastDismount : MonoBehaviour
{
    [SerializeField] private GymnastSwing swing;
    [SerializeField] private Transform gymnast;

    [Header("Flight")]
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float groundY = -2f;        // top surface of the floor
    [SerializeField] private float halfHeight = 1f;      // centre of the body to the feet
    [SerializeField] private float bodyRadius = 0.2f;    // half the body's thickness

    private enum State { Swinging, Flying, Landed }
    private State state = State.Swinging;

    private Vector2 velocity;
    private float spinDegreesPerSecond;
    private float airRotation;

    public bool IsSwinging => state == State.Swinging;
    public ScoreResult Result { get; private set; }

    public event Action Released;
    public event Action<ScoreResult> Landed;

    private void Update()
    {
        if (state == State.Swinging && ReleasePressedThisFrame())
            Release();
        else if (state == State.Landed && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void Release()
    {
        // On a circle, velocity is perpendicular to the radius: v = w x r.
        // In 2D with counter-clockwise positive rotation that is (-w * r.y, w * r.x).
        Vector2 r = gymnast.position - swing.transform.position;
        float w = swing.AngularVelocity;
        velocity = new Vector2(-w * r.y, w * r.x);
        spinDegreesPerSecond = w * Mathf.Rad2Deg;   // the body keeps rotating at the swing speed

        swing.enabled = false;                       // stops the pendulum and tap input
        gymnast.SetParent(null, true);               // detach from the bar, keep world position
        state = State.Flying;
        Released?.Invoke();
    }

    private void FixedUpdate()
    {
        if (state != State.Flying)
            return;

        float dt = Time.fixedDeltaTime;
        velocity.y -= gravity * dt;
        gymnast.position += (Vector3)(velocity * dt);

        float spinStep = spinDegreesPerSecond * dt;
        gymnast.Rotate(0f, 0f, spinStep);
        airRotation += spinStep;

        bool fallingDown = velocity.y < 0f;
        if (fallingDown && LowestPointY() <= groundY)
            Land();
    }

    /// <summary>Lowest point of the body, whatever angle it is rotated to.</summary>
    private float LowestPointY()
    {
        Vector3 up = gymnast.up;
        float verticalExtent = halfHeight * Mathf.Abs(up.y) + bodyRadius * Mathf.Abs(up.x);
        return gymnast.position.y - verticalExtent;
    }

    private void Land()
    {
        state = State.Landed;
        float landingError = Mathf.Abs(Mathf.DeltaAngle(0f, gymnast.eulerAngles.z));

        // Snap to a clean final pose: standing for a landing, lying down for a fall.
        Vector3 p = gymnast.position;
        if (ScoreJudge.IsFall(landingError))
        {
            gymnast.rotation = Quaternion.Euler(0f, 0f, 90f);
            p.y = groundY + bodyRadius;
        }
        else
        {
            gymnast.rotation = Quaternion.identity;
            p.y = groundY + halfHeight;
        }
        gymnast.position = p;

        Result = ScoreJudge.Evaluate(swing.FullTurns, airRotation, landingError, swing.MissCount);
        Landed?.Invoke(Result);
    }

    private static bool ReleasePressedThisFrame()
    {
        bool enter = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
        bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        return enter || rightClick;
    }
}
