using System;
using System.Collections;
using GymnastGame.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Connects the player and the game to the GymnastSimulation: reads input in Update, steps the
/// simulation in FixedUpdate, and raises gameplay events for the HUD and other presentation.
/// After touchdown it scores the routine and plays the landing animation; Landed is raised
/// when the animation has finished. Put this on the Bar.
/// </summary>
public class GymnastController : MonoBehaviour
{
    [SerializeField] private Transform bar;
    [SerializeField] private SimulationSettings settings = new SimulationSettings();

    [Header("Landing animation")]
    [SerializeField] private float stickDuration = 0.35f;
    [SerializeField] private float stickCrouch = 0.12f;     // how far the knees give, as a share of the body
    [SerializeField] private float stepDuration = 0.45f;
    [SerializeField] private float stepDistance = 0.3f;     // metres, about one foot
    [SerializeField] private float fallDuration = 0.6f;

    private GymnastSimulation simulation;

    // Input is read every frame but used in the next fixed step, so no tap is lost between steps.
    private bool pumpRequested;
    private bool releaseRequested;

    private bool isLanding;
    private GymnastPose landingPose;

    public bool IsSwinging => simulation.Phase == GymnastPhase.OnBar;
    public bool HasLanded { get; private set; }
    public int FullTurns => simulation.FullTurns;
    public int MissCount => simulation.MissCount;
    public ScoreResult Result { get; private set; }

    /// <summary>The pose to draw this frame: the simulation, or the landing animation once it has started.</summary>
    public GymnastPose CurrentPose => isLanding || HasLanded ? landingPose : simulation.Pose;

    public event Action<float> Pumped;
    public event Action<int> FullTurnCompleted;
    public event Action Released;
    public event Action<ScoreResult> Landed;

    private void Awake()
    {
        simulation = new GymnastSimulation(settings, bar.position);
    }

    // The simulation is created and owned here, so these subscriptions live as long as this component.
    private void OnEnable()
    {
        simulation.Pumped += HandlePumped;
        simulation.FullTurnCompleted += HandleFullTurn;
        simulation.Released += HandleReleased;
        simulation.TouchedDown += HandleTouchedDown;
    }

    private void OnDisable()
    {
        simulation.Pumped -= HandlePumped;
        simulation.FullTurnCompleted -= HandleFullTurn;
        simulation.Released -= HandleReleased;
        simulation.TouchedDown -= HandleTouchedDown;
    }

    private void Update()
    {
        if (!IsSwinging) return;
        pumpRequested |= TapPressedThisFrame();
        releaseRequested |= ReleasePressedThisFrame();
    }

    private void FixedUpdate()
    {
        simulation.Step(Time.fixedDeltaTime, new GymnastCommands(pumpRequested, releaseRequested));
        pumpRequested = false;
        releaseRequested = false;
    }

    private void HandlePumped(float quality) => Pumped?.Invoke(quality);
    private void HandleFullTurn(int turns) => FullTurnCompleted?.Invoke(turns);
    private void HandleReleased() => Released?.Invoke();

    private void HandleTouchedDown(TouchdownInfo touchdown)
    {
        float landingError = Mathf.Abs(touchdown.BodyAngleDegrees);
        Result = ScoreJudge.Evaluate(simulation.FullTurns, touchdown.AirRotationDegrees, landingError, simulation.MissCount);

        landingPose = new GymnastPose(touchdown.Position, touchdown.BodyAngleDegrees);
        isLanding = true;

        // A coroutine runs a little every frame: each "yield return null" waits for the next frame.
        if (ScoreJudge.IsFall(landingError)) StartCoroutine(PlayFall(touchdown));
        else if (landingError > ScoreJudge.StuckTolerance) StartCoroutine(PlayStep(touchdown));
        else StartCoroutine(PlayStick(touchdown));
    }

    // Knees give a little, then the body straightens up where it landed.
    private IEnumerator PlayStick(TouchdownInfo touchdown)
    {
        Vector2 standing = new Vector2(touchdown.Position.x, settings.GroundY + settings.HalfHeight);
        yield return Animate(stickDuration, t =>
        {
            float crouch = stickCrouch * Mathf.Sin(t * Mathf.PI);   // down and back up
            Vector2 p = Vector2.Lerp(touchdown.Position, standing, t);
            p.y -= crouch * settings.HalfHeight;                    // keep the feet on the mat
            return new GymnastPose(p, Mathf.Lerp(touchdown.BodyAngleDegrees, 0f, t), crouch);
        });
        FinishLanding();
    }

    // One step in the direction of travel while straightening up.
    private IEnumerator PlayStep(TouchdownInfo touchdown)
    {
        float direction = Mathf.Sign(touchdown.Velocity.x == 0f ? 1f : touchdown.Velocity.x);
        Vector2 standing = new Vector2(touchdown.Position.x + direction * stepDistance, settings.GroundY + settings.HalfHeight);
        yield return Animate(stepDuration, t =>
        {
            float eased = Mathf.SmoothStep(0f, 1f, t);
            return new GymnastPose(Vector2.Lerp(touchdown.Position, standing, eased),
                Mathf.Lerp(touchdown.BodyAngleDegrees, 0f, eased));
        });
        FinishLanding();
    }

    // Tips over to the side the body was already leaning, and ends lying on the mat.
    private IEnumerator PlayFall(TouchdownInfo touchdown)
    {
        float side = Mathf.Sign(touchdown.BodyAngleDegrees);
        Vector2 lying = new Vector2(touchdown.Position.x, settings.GroundY + settings.BodyRadius);
        yield return Animate(fallDuration, t =>
        {
            float eased = t * t;    // speeds up like a real topple
            return new GymnastPose(Vector2.Lerp(touchdown.Position, lying, eased),
                Mathf.Lerp(touchdown.BodyAngleDegrees, side * 90f, eased));
        });
        FinishLanding();
    }

    // Runs poseAt(t) every frame for t going from 0 to 1 over the given duration.
    private IEnumerator Animate(float duration, Func<float, GymnastPose> poseAt)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            landingPose = poseAt(elapsed / duration);
            yield return null;
        }
        landingPose = poseAt(1f);
    }

    private void FinishLanding()
    {
        isLanding = false;
        HasLanded = true;
        Landed?.Invoke(Result);
    }

    private static bool TapPressedThisFrame()
    {
        bool pointer = Pointer.current != null && Pointer.current.press.wasPressedThisFrame; // mouse or touch
        bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        return pointer || space;
    }

    private static bool ReleasePressedThisFrame()
    {
        bool enter = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
        bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        return enter || rightClick;
    }
}
