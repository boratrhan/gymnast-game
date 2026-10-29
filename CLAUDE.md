# CLAUDE.md

Project context for Claude Code. Read this first in every session.

## What this is

A gymnastics game in **Unity 6 (6000.6.7f1)** and **C#**, built by Bora Tarhan, a software engineer and former Turkish National Team artistic gymnast. It started as a portfolio prototype for a Dream Games (Royal Match) new-grad application. It is now growing into the long-term goal below.

**Long-term goal:** a high bar (men's horizontal bar) game judged by **real artistic gymnastics rules** (FIG Code of Points), with:
- every element and its difficulty value,
- legitimate execution deductions measured from the body's actual pose,
- connection bonuses and composition requirements,
- later, an articulated human figure and gymnast attributes (strength, rotation power, stamina).

**Scope strategy:** build the architecture for the big game, but grow the content in small steps. Start with the high bar only and about 8–10 elements, then expand.

## Working with Bora

- He is new to Unity and C#. He has a strong Java/TypeScript/OOP background. **Explain Unity concepts the first time they appear** (e.g. FixedUpdate vs Update, coroutines, ScriptableObjects, prefabs), briefly.
- He will be asked about this code in interviews. Prefer clear, idiomatic code over clever code, and make sure he understands each change.
- Communicate in **Turkish**. Code, comments, commit messages and README stay in **English**.
- He knows gymnastics rules far better than any model does. When a rule or deduction is ambiguous, **ask him** instead of guessing.

## Current state (playable prototype)

Gameplay loop: swing → pump with timed taps → giant swings → release → in-air rotation → landing → D + E score → retry.

```
Assets/
├── Scripts/Simulation/ GymnastSimulation (own assembly, pure C#): planar pendulum + pump, release (v = ω × r),
│                       projectile flight with spin, touchdown. Own clock, deterministic, unit-tested.
├── Scripts/Gameplay/   GymnastController: input in Update, Step in FixedUpdate, scores with ScoreJudge, plays the
│                       landing motion (`LandingMotion` in Simulation: stick / step / fall, stepped in Update). Events: Pumped, FullTurnCompleted,
│                       Released, Landed(ScoreResult) after the animation. RoundFlow: R = restart.
├── Scripts/Presentation/ GymnastView (draws CurrentPose), CameraFollow.
├── ScoreJudge.cs       Pure static C# scoring: ScoreResult struct, D + E, deduction constants and landing tolerances.
├── SwingHUD.cs         OnGUI prototype HUD: PERFECT/GOOD/OK/MISS, giant swing counter, controls hint, score card.
└── Main.unity          The only scene.
```

Scene hierarchy (Main.unity), real scale in metres, built by `Tools → Gymnast → Build 3D Scene` (`Assets/Editor/GymnastSceneBuilder.cs`, safe to re-run):
- `Bar` at (0, 2.6, 0), the swing pivot (does not rotate): has `GymnastController` (settings: armLength 1.1, pumpStrength 1.6, groundY 0) and `RoundFlow`.
- `Gymnast`: root-level object at the centre of mass with `GymnastView`; child bones with Humanoid names (`Hips`, `Spine`, `Head`, `Left/RightUpperArm`, `LowerArm`, `Hand`, `UpperLeg`, `LowerLeg`, `Foot`), each with a primitive visual. Sizes from `Assets/Data/Body/body.json` (1.70 m, Winter proportions); the pendulum length (hands → CoM, about 1.01 m) is derived from it.
- `BarVisual`: the visible bar, cylinder along Z. `Apparatus` holds the two uprights.
- `LandingMat`: top surface at y = 0. `Floor`: plane below it.
- `HUD`: has `SwingHUD`, with its Gymnast field pointing to the `GymnastController` on `Bar`.
- `Main Camera`: perspective, FOV 40, with `CameraFollow` (side view, about 25° off axis, follows between bar and gymnast).
- Rendering: URP, one pipeline asset `Assets/Settings/URP-Gymnast` (all quality levels use the Graphics default). URP Lit materials in `Assets/Materials`.

Controls: Space or left click = pump, Enter or right click = release, R = restart (scene must be in Build Profiles → Scene List).

Current scoring placeholder (to be replaced by Code of Points data):
- D: +0.5 per giant swing, +1.0 per full air salto.
- E: start 10.0, −0.1 per missed tap (max −1.0), −0.3 small step (15°–45° off upright), −1.0 fall (> 45°). Stuck landing is ≤ 15°.

## Architecture principles (keep these)

1. **Hand-written physics in FixedUpdate**, not Rigidbody, for deterministic and tunable motion. Input is read in Update.
2. **Gameplay → events → presentation.** Gameplay scripts raise C# events. UI, audio and effects only subscribe (observer pattern). Subscribe in OnEnable and unsubscribe in OnDisable.
3. **Rules are pure C#**, with no MonoBehaviour and no Unity scene dependencies, so they can be unit-tested (Unity Test Framework is already in Packages).
4. **Data over code** for gymnastics content. Elements, values, groups and deductions should live in ScriptableObjects or data files, never as hard-coded `if` chains. Adding an element should mean adding data, not code.
5. `[SerializeField] private` fields for Inspector tuning. Avoid public fields.
6. Avoid expensive calls (Debug.Log, GetComponent, Find) in Update and FixedUpdate.

## Roadmap

**Goal:** the gymnast can perform elements from every element group of the high bar tables (EG I swings and turns, EG II flights, EG III in-bar and Adler elements, EG IV dismounts), in routines of several elements, judged by the data in `Assets/Data/HighBar/`. Today only dismounts (EG IV) are possible.

Step numbers below are the ones used in conversations with Bora. One commit per step, game playable after each.

Done:
- ~~Code of Points analysis~~: `Assets/Data/HighBar/` (elements, deductions, composition). Do not copy the rulebook text into the game. Use the rules as mechanics only. Do not use the FIG logo or name as branding.
- ~~Data model and D-score engine~~: `Assets/Scripts/Rules/` (own assembly) with EditMode tests.
- ~~1. URP, perspective camera, real-scale 3D scene~~.
- ~~2. Split into simulation, controller, view and round flow, with an animated landing~~.
- ~~3. Articulated figure, BodyPose, tuck and pike with angular momentum~~. Shape change time (0.15 s) and height (1.70 m) are in body.json, to tune in play.
- ~~4. Twist in flight~~: D held builds twist momentum H, rate = H / I_long (3 turns/s with arms on the chest, in body.json). Piked twisting is slowest, confirmed by Bora as realistic. Twist error is measured from the nearest half twist until elements are recognised; incompleteTwist in deductions.json (5° tolerance is game-interpretation). Placeholder D +0.5 per full twist.

Next:
5. ~~Regrasp: catch the bar after a flight~~ done. "Close enough holds, faults cost deductions": Enter in the air catches when the hands are within 0.6 m of the bar (0.2 s input buffer), and holding Enter catches as soon as they come within reach (holding alone reports no failure); only more than 0.6 m, more than 4 m/s towards or away from the bar (slip) or pressing right after the release stop a catch (body.json "catch"). The body keeps its speed around the bar (ω = v_around / L, not angular momentum, so far catches give no speed bonus) and reaches for the bar over 0.12 s (the simulation blends the drawn pose). The simulation only measures the catch (`CatchQuality`: reach, leg bend, arm deviation, twist error, impact); `RegraspJudge` (Rules) turns it into deductions with regraspReach, regraspNotOpened (both game-interpretation), bentArmsRegrasp and incompleteTwist from deductions.json, shown as separate "catch N:" lines. `CatchIndicator` colours the bar and hands: green = clean catch, yellow = holds with deductions. T toggles 0.5x slow motion (`RoundFlow`, Time.timeScale). Facing flips after an odd number of half twists. Each flight is a `FlightRecord`. Placeholder D adds up saltos and twists of all flights. No bar collision yet.
5b. **Counter-rotation flights (Tkatchev, Piatti, Markelov, Yamawaki families).** The body must stop or reverse its rotation at release (a beat with hips and shoulders), which the current release cannot do. **Ask Bora before starting: he wants to discuss the technique first.**
6. **EG I turns and hops on the bar** (½ turns, pirouettes, Quast, Rybalko, el-grip): facing and grip state, tap = ½ turn, hold = 1/1 near the handstand; handstand deviation (strict / lenient tables).
7. **Element recognition:** turn swing and flight records into element ids from elements.json, then feed the routine to `DScoreCalculator`.
8. **EG III in-bar and Adler elements** (Stalder, Endo, Weiler, Adler, free hip circles). The figure and the swing need:
   - hip abduction (legs apart sideways) for straddle shapes, as a new BodyPose joint, with the legs' side offset in the model and a rotation about the bone's local X in the view;
   - shape changes while on the bar, so the hands-to-centre-of-mass radius changes during the swing (angular momentum about the bar instead of a fixed pendulum length);
   - deep pike and stoop positions with the feet passing between the hands, and a wider shoulder range (shoulder angle beyond 180° for Adler-type elements);
   - "through handstand" detection to apply the handstand deviation deductions.
9. **Replace the placeholder `ScoreJudge`** with the data-driven rules (DScoreCalculator for D, deductions.json for E). Add unit tests.
   - Landing deductions must follow the Code of Points (Art. 9.4): small step 0.1 each, large step 0.3, fall 1.0, max 1.0 for steps. The placeholder takes 0.3 for a small step; fix this here. Until then the stick / step / fall thresholds stay in `ScoreJudge` and `GymnastController` only classifies with them.
10. **Execution faults from the figure's joint angles** (bent arms or knees, open legs).
11. Later: gymnast attributes, mobile touch controls, a real UI (replace OnGUI), Mixamo character view.

Design constraints for all steps:
- Physics stays planar (XY plane, bar along Z); twist ψ about the body's long axis is the only extra rotation. No free 3D physics.
- Controls: Space = pump, Enter = release / catch, A = tuck (hold), S = pike (hold), D = twist (hold).

## Practical notes

- **Opening Unity:** Unity Hub fails to open or create this project on this machine (the editor exits silently). Open it from Terminal instead:
  ```bash
  "/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity" -projectPath ~/UnityProjects/GymnastGame
  ```
- **Compile check without Unity:** run `tools/compile-check.sh` after every code change and before asking Bora to test. It builds the Unity-generated .csproj files with the editor's bundled dotnet and picks up new .cs files. It cannot run tests: those still need Bora's Test Runner run.
- **Compile errors:** check `Logs/Editor.log` in the project. `~/Library/Logs/Unity/Editor.log` only has the startup lines; Unity 6.6 moves logging to the project folder after that.
- **IDE:** JetBrains Rider (non-commercial licence) is set as the external script editor.
- **Input:** the project uses the **Input System package** (Active Input Handling = Input System Package (New)). Use `UnityEngine.InputSystem`, not the old `Input` class.
- **Unity pitfalls for Bora:** changes made in Play mode are lost when you stop. The Game view must be clicked to receive keyboard input. Dragging a script onto the Hierarchy creates a new GameObject; use Add Component instead.

## Git

- Remote: GitHub `gymnast-game` (public, linked from Bora's CV, so keep `main` working and the README accurate).
- `.gitignore` excludes Library, Temp, Logs, obj, UserSettings and IDE files. **Always commit `.meta` files** with their assets.
- Small commits with clear English messages, e.g. `Add dismount, flight and D+E scoring`.
- Save the scene in Unity (File → Save) before committing.
- Update README.md when gameplay, controls or architecture change.
- **Never push without Bora's confirmation that the tests pass in the Test Runner.** Commit only, then ask him to run `Window → General → Test Runner → EditMode → Run All` and push after he confirms. A clean compile in `Logs/Editor.log` is not enough: tests cannot be run from the command line while the editor is open.
