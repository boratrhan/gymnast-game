using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Swings the gymnast around the bar like a pendulum.
/// Tapping near the bottom of the swing (the "pump" window) adds momentum,
/// just like a real kip/tap swing. Attach this to the Bar pivot object;
/// the gymnast sprite is a child hanging below the pivot.
/// </summary>
public class GymnastSwing : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float armLength = 2f;      // distance from bar to gymnast's centre of mass
    [SerializeField] private float damping = 0.15f;     // air/friction loss per second
    [SerializeField] private float startAngle = 30f;    // degrees, so the swing starts moving

    [Header("Pump (tap) settings")]
    [SerializeField] private float pumpStrength = 1.2f;   // angular speed added by a perfect tap (rad/s)
    [SerializeField] private float pumpWindow = 25f;      // degrees either side of the bottom where a tap counts
    [SerializeField] private float pumpCooldown = 0.35f;  // one tap per pass through the bottom

    private float angle;            // radians, 0 = hanging straight down
    private float angularVelocity;  // radians per second
    private float lastPumpTime = -10f;

    /// <summary>Current angle in degrees, wrapped to -180..180 (0 = bottom, ±180 = handstand on top).</summary>
    public float AngleDegrees => Mathf.DeltaAngle(0f, angle * Mathf.Rad2Deg);
    public float AngularVelocity => angularVelocity;

    /// <summary>Quality (0..1) of the most recent tap: 1 = exactly at the bottom.</summary>
    public float LastPumpQuality { get; private set; }

    /// <summary>Number of taps that landed outside the pump window.</summary>
    public int MissCount { get; private set; }

    /// <summary>Number of complete turns over the bar so far.</summary>
    public int FullTurns { get; private set; }

    /// <summary>Raised on every tap with its quality (0 = miss, 1 = perfect).</summary>
    public event Action<float> Pumped;

    /// <summary>Raised each time the gymnast completes a full turn over the bar.</summary>
    public event Action<int> FullTurnCompleted;

    private void Start()
    {
        angle = startAngle * Mathf.Deg2Rad;
    }

    private void Update()
    {
        if (TapPressedThisFrame())
            TryPump();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Pendulum equation: angular acceleration = -(g / L) * sin(angle)
        float angularAcceleration = -(gravity / armLength) * Mathf.Sin(angle);
        angularVelocity += angularAcceleration * dt;
        angularVelocity *= 1f - damping * dt;
        angle += angularVelocity * dt;

        transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);

        CheckFullTurn();
    }

    private void CheckFullTurn()
    {
        // angle is not wrapped, so it keeps growing with each turn.
        // A turn counts when the gymnast passes over the top of the bar: |angle| > PI, 3*PI, 5*PI ...
        int turns = Mathf.FloorToInt((Mathf.Abs(angle) + Mathf.PI) / (2f * Mathf.PI));
        if (turns > FullTurns)
        {
            FullTurns = turns;
            FullTurnCompleted?.Invoke(FullTurns);
        }
    }

    private void TryPump()
    {
        if (Time.time - lastPumpTime < pumpCooldown)
            return;
        lastPumpTime = Time.time;

        float distanceFromBottom = Mathf.Abs(AngleDegrees);
        if (distanceFromBottom > pumpWindow)
        {
            LastPumpQuality = 0f;
            MissCount++;
            Pumped?.Invoke(0f);
            return;
        }

        LastPumpQuality = 1f - distanceFromBottom / pumpWindow;
        float direction = Mathf.Sign(angularVelocity == 0f ? 1f : angularVelocity);
        angularVelocity += direction * pumpStrength * LastPumpQuality;
        Pumped?.Invoke(LastPumpQuality);
    }

    private static bool TapPressedThisFrame()
    {
        bool pointer = Pointer.current != null && Pointer.current.press.wasPressedThisFrame; // mouse or touch
        bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        return pointer || space;
    }
}