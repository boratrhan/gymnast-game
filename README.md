# Gymnastics Swing Game

A timing-based gymnastics game built with **Unity 6** and **C#**, inspired by my years as a Turkish National Team artistic gymnast (2× European Championships bronze medalist).

You swing on the high bar, build momentum with well-timed taps, let go at the right moment, and land. The routine is then scored the way real judges do it: **Difficulty (D) + Execution (E)**.

> **Status:** early prototype, in active development.

---

## How to play

| Input | Action |
|---|---|
| `Space` / left click | **Pump.** Tap as the gymnast passes the bottom of the swing to add momentum |
| `Enter` / right click | **Release.** Let go of the bar and fly |
| `R` | Try again after landing |

1. Tap at the bottom of each swing. The closer to the bottom, the stronger the push: **PERFECT**, **GOOD**, **OK** or **MISS**.
2. Build enough speed to go all the way over the bar: a **giant swing**.
3. Release and rotate in the air. Letting go while swinging down gives a forward salto.
4. Land as upright as possible.

## Scoring

Modelled on the structure of artistic gymnastics scoring:

**Difficulty (D)**: what you attempted
- +0.5 per giant swing
- +1.0 per full salto in the air

**Execution (E)**: how cleanly you did it, starting from 10.0
- −0.1 per missed tap (up to −1.0)
- −0.3 for a small step on landing (body 15°–45° off upright)
- −1.0 for a fall (more than 45° off upright)

A landing within 15° of upright is a **stuck landing**, with no deduction.

The game still uses these placeholder values. A data-driven D-score engine based on the real high bar rules is built and unit-tested (see below), and will replace them once elements are recognised in play.

## Technical overview

```
Assets/
├── GymnastSwing.cs     Pendulum physics and tap-to-pump timing
├── GymnastDismount.cs  Release, projectile flight, in-air rotation, landing
├── ScoreJudge.cs       D + E scoring rules (pure C#, no Unity dependencies)
├── SwingHUD.cs         On-screen feedback, controls and score card
├── Data/HighBar/
│   ├── elements.json     High bar element catalogue: 131 elements with value, group, family, saltos, twists, shape
│   ├── deductions.json   Execution deductions as measurable rules (angle, count or event bands)
│   └── composition.json  D-score composition: counting elements, group bonuses, connections, stick bonus, flight limit
├── Scripts/Rules/      D-score engine (own assembly, pure C#): catalogue, routine, DScoreCalculator
└── Tests/EditMode/     NUnit tests for the D-score engine, with routines calculated by hand
```

**Design decisions**

- **Hand-written physics, not Rigidbody.** The swing is a pendulum (`α = −(g / L) · sin θ`) integrated in `FixedUpdate`, so the motion is deterministic and fully tunable from the Inspector.
- **Physically consistent release.** On release, the gymnast's velocity is the tangential velocity of the swing (`v = ω × r`), and the body keeps rotating at the swing's angular velocity. Releasing at the right point in the swing really does matter.
- **Gameplay decoupled from presentation.** `GymnastSwing` and `GymnastDismount` expose C# events (`Pumped`, `FullTurnCompleted`, `Released`, `Landed`). The HUD only listens to them (observer pattern), so audio, effects or a new UI can be added without touching the gameplay code.
- **Rules separated from the engine.** `ScoreJudge` is a static, pure C# class. All scoring values live in one place and can be unit-tested without running the game.
- **Rules as data.** Elements, deductions and composition rules live in JSON files. `DScoreCalculator` reads them and applies repetition, family and flight limits, picks the best counting elements (trying every allowed selection), and adds group bonuses, connection values and the stick bonus. Adding an element means adding data, not code. Rules we had to interpret ourselves are marked `"source": "game-interpretation"`.
- **Input System package** for keyboard, mouse and touch input.

## Running the project

1. Install **Unity 6000.6.7f1** (or a compatible Unity 6 version) through Unity Hub.
2. Clone the repository and open the folder in Unity.
3. Open `Assets/Main.unity` and press **Play**.
4. Click inside the Game view once so it receives keyboard input.

## Roadmap

The long-term goal is a gymnastics game judged by **real artistic gymnastics rules**:

- [x] High bar element catalogue and execution deductions as data files
- [ ] Load the catalogue in game and replace the placeholder scoring values
- [ ] Release moves and dismounts recognised as named elements
- [ ] Articulated human figure, so execution faults (bent knees, bent arms, open legs) are measured from body angles
- [x] D-score engine: best counting elements, group bonuses, connection bonuses, stick bonus, unit-tested
- [ ] Gymnast attributes such as strength, rotation power and stamina
- [ ] Mobile touch controls
- [ ] Unit tests for `ScoreJudge`

## Author

**Bora Tarhan**: software engineer and former national team artistic gymnast
[LinkedIn](https://www.linkedin.com/in/bora-tarhan-6783362a1) · [GitHub](https://github.com/boratrhan)
