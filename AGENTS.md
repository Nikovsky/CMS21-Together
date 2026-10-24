# Agent and Code Review Instructions

These instructions apply to the entire `Nikovsky/CMS21-Together` repository.
They are intended for Codex code reviews, coding agents, and contributors.

## Project purpose

CMS21 Together — Community Edition is an **unofficial** community continuation of the
multiplayer mod for Car Mechanic Simulator 2021. The objective is to stabilize the mod,
fix multiplayer and UI regressions, and eventually integrate independently developed,
license-compatible fixes into the main mod so a separate fixes mod is not required.

Do not represent this project as an official game, Steam, or original-author release.

## Repository facts and boundaries

- `CMS21-Together.csproj` is a classic C# project targeting **.NET Framework 4.7.2**,
  with an x64 target and explicitly listed `<Compile Include="...">` source files.
  When adding a new C# file, update the project file as needed.
- The mod uses **Unity/IL2CPP**, **MelonLoader**, **Harmony**, and Steam/network transports.
- `MainMod.cs` contains mod lifecycle hooks and a branch-specific version string.
  **Do not assume one version applies to every branch or PR.** Verify source and
  dependencies for the branch under review.
- `README.md` documents MelonLoader 0.5.7 for installation; confirm the actual
  loader/game compatibility before changing or claiming supported versions.
- This repository contains code that refers to game and third-party assemblies in
  `Libs/`. Treat those references as environment-sensitive. Do not add or redistribute
  proprietary game DLLs, credentials, or other restricted files.
- The repository's `LICENSE` is MIT (original copyright notice must be preserved).
  External fixes, including FixForTogether, may have **different license terms**:
  review the actual license and permissions before reusing code; if not compatible,
  implement a clean, independent fix instead.

## Code areas to inspect

- `MainMod.cs`: lifecycle, scene transitions, Steam initialization and shutdown.
- `ClientSide/Data/NewUI/`: multiplayer panels, buttons, modal dialogs, navigation,
  input isolation, and UI lifecycle.
- `ClientSide/Data/Player/`: player and inventory state/synchronization.
- `ClientSide/Data/Garage/`: vehicle, part, tool, and garage synchronization.
- `ClientSide/Data/SaveSystem.cs` and `SaveSystemHooks.cs`: save/load handling.
- `ClientSide/Transports/` and `ServerSide/`: connectivity, host/client behavior,
  connection teardown, transport and packet processing.
- `Shared/`: data models, packets, serialization, protocol compatibility.

These paths describe the checked `MainMod` project structure; inspect the actual
files in the PR before making claims.

## Implementation guidelines

1. **Understand before changing.** Examine affected code paths, related Harmony
   patches, logs, and reproduction steps. Label an unverified root cause as a
   hypothesis rather than a confirmed defect.
2. Prefer **small, focused, reversible** changes with a regression test plan.
   Avoid unrelated refactors, broad formatting-only rewrites, or new dependencies
   unless justified.
3. Respect Unity/IL2CPP and MelonLoader lifecycles: avoid stale scene references,
   duplicate registration/hooks, work against destroyed Unity objects, and unsafe
   cross-thread use of Unity APIs.
4. Keep Harmony patch targets, overloads, initialization order, and original
   behavior in mind. Explain any patch that changes base-game state.
5. Preserve correct host/client authority, packet ordering, disconnection and
   reconnection behavior. Check null/disposed states and malformed or unexpected
   network data at trust boundaries.
6. For UI changes, check panel visibility, selection/input focus, modal dialogs,
   scene re-entry, and restoration of vanilla menu behavior. Hidden vanilla
   controls must not remain interactable through keyboard input.
7. For save, inventory, and vehicle changes, guard against duplication,
   desynchronization, destructive overwrite, and backwards-incompatible
   serialization. Do not silently change save or packet formats.
8. Never log secrets or publish unredacted player identifiers, lobby tokens,
   IP addresses, or private save data. Keep diagnostics scoped and removable.
9. Keep commit messages and branch names in English. Do not claim a build or
   runtime test succeeded without evidence.

## Priorities for Codex code review

Review **correctness and regressions first**, rather than style preferences.
Look specifically for:

- **Critical:** save corruption/data loss, unsafe file handling, exploitable
  trust-boundary violations, or irreversible inventory/vehicle mutations.
- **High:** crashes, broken Host/Join, Steam session failures, UI input leakage,
  deadlocks/thread-safety issues, and host/client state divergence.
- **Medium:** missing cleanup on scene change or disconnect, incomplete
  failure handling, behavioral regressions in navigation, and absent
  compatibility handling for changed packets or saves.
- **Low:** maintainability concerns only when they plausibly lead to defects.

For each finding, identify the relevant **file and line(s)**, triggering
conditions, observable impact, and an actionable fix. Distinguish a
confirmed code defect from a risk that still requires reproduction.
Do not report speculative problems as established facts.
Avoid duplicate comments on the same root cause.

## Build and verification expectations

- Use the repository's Visual Studio/MSBuild project and correct local
  dependencies. A code review is **not** a successful build.
- For UI changes, check `Host`, `Join`, `Back`, `Cancel`, keyboard/mouse
  navigation, modal dialogs, and repeated Menu → Garage → Menu transitions.
- For multiplayer changes, test **two separate PCs using the Steam version**
  of Car Mechanic Simulator 2021 where feasible. Cover host and client,
  connect/disconnect/reconnect, lobby transitions, synchronized objects,
  inventory, and save/load as appropriate.
- Distinguish tests actually run from tests still required. Local checks,
  a Release build, TCP tests, and Steam-to-Steam multiplayer tests are
  separate forms of evidence.
- Attach sanitized `MelonLoader/Latest.log`, relevant reproduction steps,
  and screenshots when diagnosing runtime failures. Back up saves before tests.

## Pull request review checklist

- [ ] Scope and affected components are described.
- [ ] Licensing and any new dependencies have been checked.
- [ ] Relevant build result is reported (or explicitly marked not run).
- [ ] UI, multiplayer, and save regression risks are assessed.
- [ ] Two-PC Steam tests are reported when applicable (or marked pending).
- [ ] Sensitive data and game-proprietary files are not added.
- [ ] Known limitations, follow-up issues, and rollback strategy are noted.

The final decision to merge is a maintainer decision. An automated
review cannot certify runtime behavior, security, or game compatibility.
