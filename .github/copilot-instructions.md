# battrail

Unity project (Editor 6000.4.8f1 / Unity 6, URP) for a local 1v1 racing-combat game: race along a
spline course in split-screen, knock the opponent off with boosted trail attacks. P2 can be a human
or an NPC. Web build (development) is published to GitHub Pages:
https://fukanojuko.github.io/battrail/Default%20WebGL/index.html

## Read these first

`docs/spec.md` and `docs/tuning.md` are the authoritative design docs (Japanese) and are kept
current with the code. Consult them before changing gameplay:

- `docs/spec.md` — the full spec plus a decision log: why boost re-ignition has a hysteresis
  threshold, why the attacker is decided by `s` ordering instead of XOR of boost flags, how the NPC
  intent table is derived from the game's payoff structure, and the Web rendering pitfalls below.
- `docs/tuning.md` — a "which asset holds this knob" index. Every tunable is listed with its
  current value and the prefab/scene/asset that owns it.

`docs/spec.md` "関連ファイル" still lists pre-refactor flat paths (`Assets/Scripts/Racer.cs`);
scripts now live in per-module folders (below). Treat the prose as current and the paths as stale.

## Build / test

There is no CLI build or test suite — the project is built exclusively via Unity Editor / Unity
Cloud Build. Do not invent `dotnet build`, npm scripts, or similar; `Assembly-CSharp*.csproj` are
Unity-generated and must not be hand-edited.

- `com.unity.test-framework` is a package dependency but no `Tests` folder exists under `Assets/`.
  If adding tests, use standard Unity Test Framework conventions (EditMode/PlayMode asmdef under a
  `Tests` folder, referencing the `Battrail.*` assemblies) and run them from the Editor's Test
  Runner window — single tests included. There is no headless CI test job.
- Audio and art binaries (`.mp3` / `.wav` / `.ogg`, FBX, textures) are Git LFS tracked via
  `.gitattributes`. `*.sh` is forced to LF because Cloud Build runs `CloudBuild/post-build.sh` under
  Cygwin on the Windows builder.

CI (`.github/workflows/`) handles versioning/release/deploy plumbing only, not build validation:

- `tag-release.yml`: on push to `main`, tags `vX.Y.Z` from `ProjectSettings/ProjectSettings.asset`
  `bundleVersion` and creates a GitHub Release, then calls `unity-build.yml`.
- `unity-build.yml`: triggers Unity Cloud Build Automation for `windows` and `web` (fire-and-forget).
- `CloudBuild/post-build.sh`: runs on Unity's build machine. Zips the player, attaches it to the
  matching Release, and for web builds force-pushes an orphan commit to `gh-pages` and dispatches
  `deploy-pages.yml`. Only `main` builds are published.
- `build-notes.yml`: appends a row to the Release body's Builds table via `repository_dispatch`
  (serialized with `concurrency` since windows/web builds can finish close together).
- `bump-version.yml`: manual `workflow_dispatch` that bumps `bundleVersion` and opens a PR to `main`.
- `sync-develop.yml`: after `main` changes, opens a PR merging `main` back into `develop`.
- When editing these, preserve the extensive Japanese comments explaining why (event ordering,
  GitHub Actions trigger quirks, API limits) — they document constraints found the hard way.

## Architecture

Two scenes only: `Title.unity` (menu, mode select) and `Boot.unity` (the race). Scene names are
passed as serialized string fields (`nextScene`, `titleScene`), not hardcoded — check the Inspector
fields when changing scene flow.

### Assembly / namespace layout

`Assets/Scripts/` is split into four assemblies whose references form a DAG. Namespace matches the
asmdef `rootNamespace`, so adding a file to a folder decides both.

```
Core        (Battrail.Core)        no references — GameMode, GraphicsCapabilityLog
 └─ Racing  (Battrail.Racing)      + Splines, Mathematics, InputSystem, VFX Graph
     │                             simulation: Racer, CourseSpline, CombatManager, RaceManager
     │      Racing/Input (Battrail.Racing.Input) — same assembly, nested namespace
     ├─ Presentation (Battrail.Presentation) + Cinemachine, UGUI — camera/BGM/screen effects
     └─ UI  (Battrail.UI)          + Core, InputSystem — UI Toolkit HUD, title, pause, post-race
```

Dependencies point inward only. UI and Presentation may read Racing; Racing must not reference
them. Putting a new file in the wrong folder silently gives it the wrong namespace and assembly
access.

### Core simulation model

A Racer's position is not a raw world Transform — it is an `(s, t)` pair relative to the course
spline (`s` = arc-length along the track, `t` = lateral offset from center). World position/rotation
is derived from `(s, t)` via `CourseSpline` every frame. This indirection makes progress/ranking,
curvature-based speed limits, and combat range checks (`Δs`, `Δt`) scalar math instead of vector
geometry.

- `CourseSpline`: wraps a `SplineContainer`, precomputes a curvature table (curvature lowers the
  max cornering speed only — there is no cornering physics), and generates the road mesh.
  `[ExecuteAlways]` so it rebuilds in the Editor. Default knots come from `DefaultKnots` only when
  the container is empty; Editor-edited knots win.
- `Racer`: owns `(s, t)`, accel/brake/boost gauge, and lateral spring-damper drift. The `Rigidbody`
  is kinematic and moved with `MovePosition`, so the `BoxCollider` on `Player.prefab` participates
  in nothing — widening it does not widen hit detection. There is no `OnCollision`/`OnTrigger`/
  `Physics.` usage anywhere in `Assets/Scripts/`.
- `IRacerInput` (`Racing/Input/`): the only thing `Racer` polls each `FixedUpdate` (forward, lateral,
  boost). Implementations: `RacerInput` (Keyboard/`Gamepad.all[index]`) and `AiRacerInput` (NPC,
  tuned via the serializable `NpcTuning` exposed on `NpcSetup` so NPC knobs stay off the
  player-facing component). Future networked/remote input implements this interface rather than
  modifying `Racer`. `Racer` creates its default input in `Start` (`_input ??=`), not `Awake`, so
  `NpcSetup.Awake` injection always wins.
- `CombatManager` + `IHitReaction`/`HitContext`/`DefaultHitReaction`: hits are computed centrally in
  `(s, t)` space (one place walks all Racer pairs) and call back into the Racer; the reaction is
  swappable independently of detection. The attacker is the racer that is behind in `s` and
  boosting.
- `RaceManager`: owns `RacePhase` (`Countdown` → `Running` → `Finished`) and pushes it to every
  Racer. HUD, `RaceBgm`, and `PostRaceController` subscribe to `RaceStarted`/`RaceFinished` rather
  than polling; new phase-dependent behavior should subscribe the same way. `CombatManager` only
  resolves during `Running`. `Racer`'s phase defaults to `Countdown` so a forgotten call fails safe.
- `GameMode` (Core): `static` carrier of the 1P-vs-NPC / 2P choice from Title to Boot. Because
  domain reload can be disabled, it is force-reset via
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` — follow this
  for any other cross-scene static state.
- Split-screen: Main Camera (viewport left) / Camera 2 (right), with Cinemachine v3
  `OutputChannel`/`ChannelMask` routing vcams. `CinemachineBrain` update method must stay
  `LateUpdate` (SmartUpdate picks FixedUpdate and reads pre-interpolation positions). Per-camera
  screen-space Canvases are hidden from the other camera via layers `P1 View`(8) / `P2 View`(9) —
  new camera-child UI needs that layer setup too.

### Web build constraints

Symptoms that only appear on Web usually come from these two, both documented in `docs/spec.md`:

- VFX Graph requires compute shaders, so trail and hit effects render nothing on WebGL 2.0. The
  Graphics API list is WebGPU → WebGL 2.0 so capable browsers get WebGPU; `GraphicsCapabilityLog`
  warns in the browser console when it falls back.
- Web uses the Mobile quality level (`Mobile_RPAsset`), not `PC_RPAsset`. The trail VFX needs
  `_CameraDepthTexture` for soft particles, which is why Depth Texture is enabled on
  `Mobile_RPAsset`. When PC and Web look different, diff those two assets first.

## Conventions

- Tune values in the scene/prefab, not in code. Changing a `[SerializeField]` initializer does not
  affect already-serialized components — the initializer only applies to newly added components.
  This previously left widened `CombatManager` hit ranges silently inactive in `Boot.unity`. Edit
  the Inspector value and keep the code initializer matching, and find the owning asset in
  `docs/tuning.md`.
- `[SerializeField] [Tooltip("...")]` for private tunables instead of public fields. Tooltips often
  cross-reference numeric constants in other scripts (e.g. `CombatManager` ranges vs `Racer` gauge
  rates); keep both sides accurate when tuning either.
- XML `///` doc comments in Japanese on nearly every class/public member, explaining why a value or
  design exists (tuning rationale, relation to other systems' constants) rather than restating the
  member name. Match this style and language.
- Prefer C# `event` / Unity 6 `Awaitable` over per-frame `Update` polling for one-shot state
  changes (`RaceManager` countdown, `PostRaceController`, `RaceBgm` fade). Continuous per-frame work
  (physics in `FixedUpdate`, HUD gauge readout) stays in `Update`/`FixedUpdate`.
- No new external dependencies — event/`Awaitable` instead of Reactive libraries, by policy.
- Player prefabs: `Assets/Prefabs/Players/Player.prefab` is the base holding logic and shared
  structure; `Player1.prefab`/`Player2.prefab` are Prefab Variants differing only in materials.
  `playerIndex` and `startLateralOffset` are scene-instance overrides in `Boot.unity`, deliberately
  not in the Variants. Artist-owned assets live under `Assets/Contents/Artist/` — adjust VFX size
  and lifetime on the component override rather than editing the `.vfx`.
- HUD is UI Toolkit (`Assets/UI/*.uxml` / `*.uss`). Overlays (`countdown`, `result`, `pause`) are
  shown by adding/removing the `hidden` class.
- Branches: `feature/…`, `fix/…`, `chore/…`, `refactor/…` off `develop`; `develop` → `main` at
  milestones. `main` is the release branch and triggers tag + Cloud Build on push. Commit subjects
  are short imperative English sentences ("Split scripts into per-module folders and namespaces").
