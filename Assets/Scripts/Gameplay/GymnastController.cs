using System;
using System.Collections.Generic;
using GymnastGame.Rules;
using GymnastGame.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>What the catch indicator shows: no catch possible, a catch with deductions, or a clean catch.</summary>
public enum CatchPreview
{
    None,
    WithDeductions,
    Clean
}

/// <summary>
/// Connects the player and the game to the GymnastSimulation: reads input in Update, steps the
/// simulation in FixedUpdate, and raises gameplay events for the HUD and other presentation.
/// After touchdown it scores the routine and plays the landing motion; Landed is raised
/// when the motion has finished. Put this on the Bar.
/// DefaultExecutionOrder makes Unity run this Update before GymnastView's, so the figure always
/// draws this frame's pose.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GymnastController : MonoBehaviour
{
    [SerializeField] private Transform bar;
    [SerializeField] private TextAsset bodyData;        // Assets/Data/Body/body.json
    [SerializeField] private TextAsset deductionData;   // Assets/Data/HighBar/deductions.json
    [SerializeField] private SimulationSettings settings = new SimulationSettings();
    [SerializeField] private LandingSettings landing = new LandingSettings();

    private GymnastSimulation simulation;

    // Input is read every frame but used in the next fixed step, so no tap is lost between steps.
    private bool pumpRequested;
    private bool releaseOrCatchRequested;
    private BodyShape requestedShape = BodyShape.Straight;
    private bool twistHeld;
    private bool catchHeld;

    private DeductionRule incompleteTwist;
    private RegraspJudge regraspJudge;
    private readonly List<ScoreLine> regraspDeductions = new List<ScoreLine>();

    private LandingMotion landingMotion;    // null until touchdown

    public BodyModel Body { get; private set; }
    public bool IsSwinging => simulation.Phase == GymnastPhase.OnBar;
    public bool IsFlying => simulation.Phase == GymnastPhase.Flying;
    public IReadOnlyList<FlightRecord> Flights => simulation.Flights;
    public CatchStatus CatchStatus => simulation.CatchStatus;

    /// <summary>Whether Enter would catch the bar now, and whether that catch would be clean.</summary>
    public CatchPreview CatchPreview
    {
        get
        {
            var status = simulation.CatchStatus;
            if (!status.CanCatch) return CatchPreview.None;
            var q = status.Quality;
            return regraspJudge.IsClean(q.Reach, q.LegBend, q.ArmDeviation, q.TwistError)
                ? CatchPreview.Clean
                : CatchPreview.WithDeductions;
        }
    }
    public bool HasLanded { get; private set; }
    public int FullTurns => simulation.FullTurns;
    public int MissCount => simulation.MissCount;
    public ScoreResult Result { get; private set; }

    /// <summary>The pose to draw this frame: the simulation, or the landing motion once it has started.</summary>
    public GymnastPose CurrentPose => landingMotion != null ? landingMotion.Pose : simulation.Pose;

    public event Action<float> Pumped;
    public event Action<int> FullTurnCompleted;
    public event Action Released;
    public event Action<FlightRecord> Regrasped;
    public event Action CatchSlipped;
    public event Action<CatchBlocker> CatchFailed;
    public event Action<ScoreResult> Landed;

    private void Awake()
    {
        // A TextAsset is a text file in the project that Unity loads for us, like a resource.
        Body = BodyModel.FromJson(bodyData.text);
        var deductions = DeductionTable.FromJson(deductionData.text);
        incompleteTwist = deductions.Get("incompleteTwist");
        regraspJudge = new RegraspJudge(deductions);
        simulation = new GymnastSimulation(settings, Body, bar.position);
    }

    // The simulation is created and owned here, so these subscriptions live as long as this component.
    private void OnEnable()
    {
        simulation.Pumped += HandlePumped;
        simulation.FullTurnCompleted += HandleFullTurn;
        simulation.Released += HandleReleased;
        simulation.Regrasped += HandleRegrasped;
        simulation.CatchSlipped += HandleCatchSlipped;
        simulation.CatchFailed += HandleCatchFailed;
        simulation.TouchedDown += HandleTouchedDown;
    }

    private void OnDisable()
    {
        simulation.Pumped -= HandlePumped;
        simulation.FullTurnCompleted -= HandleFullTurn;
        simulation.Released -= HandleReleased;
        simulation.Regrasped -= HandleRegrasped;
        simulation.CatchSlipped -= HandleCatchSlipped;
        simulation.CatchFailed -= HandleCatchFailed;
        simulation.TouchedDown -= HandleTouchedDown;
    }

    private void Update()
    {
        if (IsSwinging) pumpRequested |= TapPressedThisFrame();
        if (IsSwinging || IsFlying) releaseOrCatchRequested |= ReleasePressedThisFrame();    // Enter: let go, or catch
        requestedShape = ShapeHeld();
        twistHeld = Keyboard.current != null && Keyboard.current.dKey.isPressed;    // D = twist, held
        catchHeld = IsFlying && ReleaseHeld();                                      // Enter held: catch when in reach

        // The landing motion is the only thing that moves the body after touchdown.
        landingMotion?.Advance(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        simulation.Step(Time.fixedDeltaTime,
            new GymnastCommands(pumpRequested, releaseOrCatchRequested, requestedShape, twistHeld, catchHeld));
        pumpRequested = false;
        releaseOrCatchRequested = false;
    }

    private void HandlePumped(float quality) => Pumped?.Invoke(quality);
    private void HandleFullTurn(int turns) => FullTurnCompleted?.Invoke(turns);
    private void HandleReleased() => Released?.Invoke();
    private void HandleRegrasped(FlightRecord flight)
    {
        // Judge the catch now, while it is fresh; the deductions go on the score card at the end.
        var q = flight.Catch;
        int number = simulation.Flights.Count;
        foreach (var item in regraspJudge.Deductions(q.Reach, q.LegBend, q.ArmDeviation, q.TwistError))
            regraspDeductions.Add(new ScoreLine(-(float)item.Points, $"catch {number}: {item.Label}"));
        Regrasped?.Invoke(flight);
    }
    private void HandleCatchSlipped() => CatchSlipped?.Invoke();
    private void HandleCatchFailed(CatchBlocker reason) => CatchFailed?.Invoke(reason);

    private void HandleTouchedDown(TouchdownInfo touchdown)
    {
        float landingError = Mathf.Abs(touchdown.LandingAngleDegrees);
        // Placeholder D: saltos and full twists of every flight, caught or not.
        int saltos = 0, fullTwists = 0;
        foreach (var flight in simulation.Flights)
        {
            saltos += flight.Saltos;
            fullTwists += flight.FullTwists;
        }
        Result = ScoreJudge.Evaluate(simulation.FullTurns, saltos, landingError, simulation.MissCount,
            fullTwists, touchdown.TwistErrorDegrees, incompleteTwist, regraspDeductions);

        LandingKind kind = ScoreJudge.IsFall(landingError) ? LandingKind.Fall
            : landingError > ScoreJudge.StuckTolerance ? LandingKind.Step
            : LandingKind.Stick;
        landingMotion = new LandingMotion(Body, landing, settings.GroundY, touchdown, kind);
        landingMotion.Finished += FinishLanding;
    }

    private void FinishLanding()
    {
        landingMotion.Finished -= FinishLanding;
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

    private static bool ReleaseHeld()
    {
        bool enter = Keyboard.current != null && Keyboard.current.enterKey.isPressed;
        bool rightButton = Mouse.current != null && Mouse.current.rightButton.isPressed;
        return enter || rightButton;
    }

    // A = tuck, S = pike, held. Tuck wins if both are held.
    private static BodyShape ShapeHeld()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return BodyShape.Straight;
        if (keyboard.aKey.isPressed) return BodyShape.Tuck;
        if (keyboard.sKey.isPressed) return BodyShape.Pike;
        return BodyShape.Straight;
    }
}
