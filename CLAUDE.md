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
├── GymnastSwing.cs     Pendulum physics + tap-to-pump timing. Events: Pumped(quality), FullTurnCompleted(turns).
│                       Exposes AngleDegrees, AngularVelocity, FullTurns, MissCount.
├── GymnastDismount.cs  Release (v = ω × r), projectile flight, in-air spin, landing detection.
│                       Landing snaps to a final pose (standing or lying); no landing animation yet. Events: Released, Landed(ScoreResult).
├── ScoreJudge.cs       Pure static C# scoring: ScoreResult struct, D + E, deduction constants and landing tolerances.
├── SwingHUD.cs         OnGUI prototype HUD: PERFECT/GOOD/OK/MISS, giant swing counter, controls hint, score card.
└── Main.unity          The only scene.
```

Scene hierarchy (Main.unity), real scale in metres, built by `Tools → Gymnast → Build 3D Scene` (`Assets/Editor/GymnastSceneBuilder.cs`, safe to re-run):
- `Bar` at (0, 2.6, 0), the swing pivot: has `GymnastSwing` (armLength 1.1, pumpStrength 1.6) and `GymnastDismount` (groundY 0). Its child `Gymnast` is a 1.7 m capsule at local (0, -1.1, 0).
- `BarVisual`: the visible bar, cylinder along Z (not parented to `Bar`, which rotates). `Apparatus` holds the two uprights.
- `Ground`: the landing mat, top surface at y = 0. `Floor`: plane below it.
- `HUD`: has `SwingHUD`, with Swing and Dismount fields pointing to `Bar`.
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

## Next steps (planned order)

1. ~~Code of Points analysis~~ done: `Assets/Data/HighBar/` (elements, deductions, composition). Do not copy the rulebook text into the game. Use the rules as mechanics only. Do not use the FIG logo or name as branding.
2. ~~Data model and D-score~~ done: `Assets/Scripts/Rules/` (own assembly) with EditMode tests.
3. **3D migration** (in progress, plan approved by Bora), one commit per step, game playable after each:
   1. ~~URP, perspective camera, real-scale 3D scene, capsule figure~~ done.
   2. Split into `GymnastSimulation` (pure C#, planar physics, unit-tested), `GymnastController` (input + events), `GymnastView` (presentation), `RoundFlow` (restart). Add an animated landing in `GymnastController`.
   3. Articulated primitive figure driven by a `BodyPose` struct (Humanoid bone names, so a Mixamo view can replace it later). Body shape with angular momentum (L = I·ω).
   4. Twist in flight: hold to twist, rate depends on body shape.
   5. Regrasp: catch window around the bar, tangential velocity kept, grip from twist angle.
   6. On-bar turns and hops (tap = ½ turn, hold = 1/1 near handstand).
   - Physics stays planar (XY plane, bar along Z); twist ψ about the body's long axis is the only extra rotation. No free 3D physics.
   - Controls: Space = pump, Enter = release / catch, A = tuck (hold), S = pike (hold), D = twist (hold).
4. Element recognition: detect which element was performed from swing and flight data.
5. Replace the placeholder `ScoreJudge` values with the data-driven rules. Add unit tests for `ScoreJudge`.
6. Measure execution faults (bent arms or knees, open legs) from the figure's joint angles.
7. Later: gymnast attributes, mobile touch controls, and a real UI (replace OnGUI).

## Practical notes

- **Opening Unity:** Unity Hub fails to open or create this project on this machine (the editor exits silently). Open it from Terminal instead:
  ```bash
  "/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity" -projectPath ~/UnityProjects/GymnastGame
  ```
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
