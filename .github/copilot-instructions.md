# battrail

Unity project (Editor 6000.4.8f1 / Unity 6) for a local 2-player (or 1P vs NPC) racing-combat
game: race along a spline course, knock the opponent off with boosted trail attacks. Web build
(development) is published to GitHub Pages: https://fukanojuko.github.io/battrail/Default%20WebGL/index.html

## Build / test

There is no CLI build or test suite in this repo — it's built exclusively via Unity Editor / Unity
Cloud Build. Do not invent `dotnet build`, npm scripts, or similar; `Assembly-CSharp.csproj` and
`Assembly-CSharp-Editor.csproj` are Unity-generated and should not be hand-edited.

- All gameplay code lives directly in the default `Assembly-CSharp` assembly (no `.asmdef` split).
- `com.unity.test-framework` is a package dependency but no `Tests` folder exists under `Assets/` yet —
  if adding tests, follow standard Unity Test Framework conventions (EditMode/PlayMode asmdef under
  a `Tests` folder) and run them from the Editor's Test Runner window; there is no headless CI test job.
- CI (`.github/workflows/`) only handles versioning/release/deploy plumbing, not build validation:
  - `tag-release.yml`: on push to `main`, tags `vX.Y.Z` from `ProjectSettings/ProjectSettings.asset`
    `bundleVersion` and creates a GitHub Release, then calls `unity-build.yml`.
  - `unity-build.yml`: triggers Unity Cloud Build Automation for `windows` and `web` targets (fire-and-forget).
  - `CloudBuild/post-build.sh`: runs on Unity's build machine after each Cloud Build. Zips the
    player, attaches it to the matching GitHub Release, and for web builds also force-pushes an
    orphan commit to `gh-pages` and dispatches `deploy-pages.yml`. Only builds from the `main`
    branch are published.
  - `build-notes.yml`: appends a row to the Release body's Builds table via `repository_dispatch`
    (serialized with `concurrency` since windows/web builds can finish close together).
  - `bump-version.yml`: manual `workflow_dispatch` that bumps `bundleVersion` and opens a PR to `main`.
  - `sync-develop.yml`: after `main` changes, opens a follow-up PR to merge `main` back into `develop`.
  - When touching any of these, preserve the existing extensive Japanese comments explaining *why*
    (event ordering, GitHub Actions trigger quirks, GitHub API limits) — they document non-obvious
    constraints discovered the hard way.

## Architecture

All gameplay scripts are in `Assets/Scripts/`, namespace `Battrail`, one class per file. Only two
scenes exist: `Title.unity` (menu, mode select) and `Boot.unity` (the actual race). Scene names are
passed as serialized string fields (`nextScene`, `titleScene`), not hardcoded — check the relevant
Inspector fields when changing scene flow.

Core simulation model: Racer position is **not** a raw world Transform — it's a `(s, t)` pair
relative to the course spline (`s` = distance along the track, `t` = lateral offset from center).
World position/rotation is derived from `(s, t)` via `CourseSpline` every frame. This indirection is
what makes lap logic, curvature-based speed limits, and combat range checks (`Δs`, `Δt`) simple
scalar math instead of vector geometry.

- `CourseSpline`: wraps a `SplineContainer`, precomputes a curvature table (curvature affects max
  cornering speed), and generates the visible road mesh. `[ExecuteAlways]` so it rebuilds in Editor.
- `Racer`: owns `(s, t)` state, acceleration/braking/boost gauge, and lateral drift physics
  (kinematic `Rigidbody`). Does not read input directly.
- `IRacerInput` (in `RacerInput.cs`): abstraction Racer polls every `FixedUpdate` for
  move vector + boost button. Two implementations:
  - `RacerInput`: real device input via the new Input System (gamepad-per-index, falls back to
    WASD/arrows on keyboard).
  - `AiRacerInput`: NPC decision logic, tuned via the serializable `NpcTuning` (exposed on
    `NpcSetup`, not on `Racer`, to keep NPC-only knobs off the player-facing component).
  - Any future networked/remote input should implement this same interface rather than changing `Racer`.
- `Combat` (`CombatManager` + `IHitReaction`): computes hits centrally in `(s, t)` space (one place
  checks all Racer pairs) and calls back into `Racer.ApplyKnockback` — reaction logic (`DefaultHitReaction`)
  is swappable independent of hit detection.
- `RaceManager`: event-driven race phase (`Countdown` → `Running` → `Finished`). Other systems
  (HUD, BGM, PostRaceController) subscribe to its `RaceStarted`/`RaceFinished` events rather than
  polling; new race-phase-dependent behavior should follow the same subscribe pattern.
- `GameMode`: a `static` class carrying the P1-vs-P2 vs P1-vs-NPC choice from Title into Boot.
  Because domain reload can be disabled, its state is force-reset via
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` — follow this
  pattern for any other cross-scene static state to avoid stale values between Play sessions/builds.

## Conventions

- XML-style `///` doc comments on nearly every class/public member, written in Japanese, explaining
  *why* a value/design exists (tuning rationale, relationships to other systems' constants) rather
  than restating the member name. Match this style/language for new public members.
- `[SerializeField] [Tooltip("...")]` for private tunable fields instead of public fields; tooltips
  often cross-reference specific numeric constants in other scripts (e.g. `CombatManager` ranges) —
  keep such cross-references accurate when tuning either side.
- Branch naming: `feature/…`, `fix/…`, `chore/…`, `refactor/…` off `develop`; `main` is the release
  branch (only receives merges/PRs, triggers tag+build on push).
