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
├── GymnastDismount.cs  Release (v = ω × r), projectile flight, in-air spin, landing detection,
│                       landing animations via coroutines (stuck / small step / fall). Events: Released, Landed(ScoreResult).
├── ScoreJudge.cs       Pure static C# scoring: ScoreResult struct, D + E, deduction constants and landing tolerances.
├── SwingHUD.cs         OnGUI prototype HUD: PERFECT/GOOD/OK/MISS, giant swing counter, controls hint, score card.
└── Main.unity          The only scene.
```

Scene hierarchy (Main.unity):
- `Bar` at (0, 2, 0): has `GymnastSwing` and `GymnastDismount`. Its child `Gymnast` is a capsule at local (0, -1.2, 0), scale (0.4, 1, 0.4).
- `BarVisual`: cylinder showing the bar (not parented to Bar).
- `Ground`: cube at (0, -2.5, 0), scale (20, 1, 1). The top surface is y = -2, matching `GymnastDismount.groundY`.
- `HUD`: has `SwingHUD`, with Swing and Dismount fields pointing to `Bar`.
- `Main Camera`: orthographic, size 4, at (0, 1.5, -8).

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

1. **Analyse the FIG Code of Points** (high bar section) that Bora provides. Extract elements (name, difficulty letter and value, element group, required rotations and saltos, release or dismount) and execution deductions into a structured data file. Do not copy the rulebook text into the game. Use the rules as mechanics only. Do not use the FIG logo or name as branding.
2. Design the data model: `ElementDefinition` ScriptableObject, `DeductionRule`, element groups, and a routine or composition model.
3. Element recognition: detect which element was performed from swing and flight data.
4. Replace the placeholder `ScoreJudge` values with the data-driven rules. Add unit tests for `ScoreJudge`.
5. Articulated figure (simple ragdoll skeleton first) so execution faults (bent arms or knees, open legs) are measured from joint angles.
6. Later: gymnast attributes, mobile touch controls, and a real UI (replace OnGUI).

## Practical notes

- **Opening Unity:** Unity Hub fails to open or create this project on this machine (the editor exits silently). Open it from Terminal instead:
  ```bash
  "/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity" -projectPath ~/UnityProjects/GymnastGame
  ```
- **Compile errors:** check `~/Library/Logs/Unity/Editor.log`.
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
