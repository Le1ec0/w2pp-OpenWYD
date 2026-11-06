# MOB-AI-001 Task 5DM: Elemental Protection Type 25 Player Score Plan

> **For agentic workers:** Execute inline in the existing checkout. Use test-first steps; preserve unrelated local changes. Do not commit, push, publish, or deploy.

**Goal:** Make player self-cast skill 53 apply its audited Type 25 CurrentScore projection and relay the refreshed score to the caster.

**Architecture:** Reuse the existing 7.69 login score projector and Type 25 AC/resistance stages. Add an exact skill metadata matcher in `WorldHub`; keep the Type 25 arithmetic and wire protocol unchanged.

**Tech Stack:** C# / .NET 10.0, `WydCdk.World`, Engine.Tests, synthetic three-peer TCP smoke.

**Spec:** `docs/PORT_TRACEABILITY.md` Type 25 CurrentScore audit; TMProject 7.69 `Servidor/Server/Common/skilldata.csv` row 53 and compiled `Servidor/Source/Code/Basedef.cpp` are behavioral authority.

## Global Constraints

- Compare `Server/W2PP` for each runtime behavior; the 7.69 Type 25 branch adds AC and four resistances, while W2PP adds only Fire/Thunder/Ice resistance.
- Match only skill 53 with `InstanceType=0`, `Aggressive=0`, and `AffectType=25`.
- Preserve Type 25 projection formulas already covered by login tests; do not change persistence, schema, protocol, or generic affect behavior.
- Preserve the existing dirty checkout; no commit, push, package, publish, or deploy.
- Use SDK 10.0.401 and record `dotnet --info` with validation evidence.

## Review Focus

- Skill row and target: `TargetType=0`, value 10, CSV time 60, `AffectResist=0`; the caster receives the self affect.
- Duration: loader converts 60 to 15 ticks; Special2=50 gives delay 150 and SetAffect duration 24.
- Projection: at level 50, AC increases by 35 and each 7.69 elemental resistance increases by 2; current HP/MP stay unchanged.
- Recast and expiry: recast refreshes without stacking; timer expiry clears the affect and restores the baseline score.
- Relay scope: only the caster stream receives UpdateScore/UpdateAffect for this self-cast.

---

### Task 1: Add the audited skill 53 Type 25 runtime projection

**Files:**
- Create: `tests/WydCdk.Engine.Tests/AttackPlayerElementalProtectionAffectTests.cs`
- Modify: `tests/WydCdk.Engine.Tests/Program.cs`
- Modify: `src/WydCdk.World/WorldHub.cs`
- Modify: `tests/WydCdk.Client.E2E/MobAiNetwork.cs`
- Modify: root `AGENTS.md`, `BACKLOG.md`, `PROGRESSO.md`, `docs/PORT_TRACEABILITY.md`, and `docs/CLIENT_E2E_SMOKE.md`

- [x] Add and register a regression covering effect fields, AC/resistance projection, HP/MP, recast, and expiry; run Engine.Tests and confirm the missing runtime score projection fails (`513/514`, AC remained 100).
- [x] Add the exact Type 25 skill matcher to player score rebuilding and the audited affect selector; do not change the existing Type 25 math.
- [x] Add a three-peer TCP smoke that checks Type 25 UpdateScore/UpdateAffect reaches only the caster.
- [x] Run Engine.Tests (`514/514`), the TCP smoke, the full no-incremental solution build (0 errors; one pre-existing CS8604), and `dotnet --info` (SDK 10.0.401, exit 0).
- [x] Update the in-place project handoff, progress/backlog, and source/smoke traceability.

### Follow-up: authenticated Host, visual client, and warning — complete

**Goal:** Complement the synthetic loopback smoke with a sandbox-account Host run and a direct visual run of the isolated source-built 7.69 client, and remove the existing nullable-flow warning.

**Constraints:** Keep the dirty Server checkout intact. Bind test Host only to `127.0.0.1`; generate account/review data in a unique `Backup/Build` fixture. Do not use the active `Client/7670/WYD.exe` whose hash/provenance is unverified. Compile only in the isolated TMProject copy with explicit output/intermediate directories, then scan the selected binary before launch. The user must enter the synthetic account in the client UI manually; do not automate the authentication dialog.

- [x] Reproduce the full-solution `CS8604` at `AttackDivineBlessingTests.cs:256`; add an explicit null guard and separate assertion without changing runtime behavior. Full solution build now reports 0 warnings and 0 errors.
- [x] Add and run the Host-authenticated loopback check for skill 53 using a generated legacy account fixture; verified account, secure code, character login, and Type 25 `UpdateScore` then `UpdateAffect` through the Host. Marker: `HOST_AUTHENTICATED_TYPE25_PASS ... external-database=none`.
- [x] Build and scan the isolated TMProject client runtime with explicit output/intermediate directories; record source identity, command, result hash, and Defender result in the local build evidence.
- [x] Complete the visual Type 25 cast in the isolated client. Initial login reached a blue void because the fixture location mapped to missing Field15,15; normal exit persisted the character at Field16,16, whose client assets are available. The skill did not target because the supplied `SkillData.bin` used 104-byte records while TMProject reads 96-byte `STRUCT_SPELL` records. Repacked the fixture's 248 rows to 96-byte records, preserving the original at `Server/Backup/Build/Type25AuthE2E-20261026-05/SkillData-original-104-byte-records.bin`; the corrected 23,808-byte file reports skill 53 `MaxTarget=1`. The visual cast then passed: AC 100→135, four resistances 0→2, HP stayed 1000, MP 1000→869; Host received skill 53 self-target and sent Type25 score then affect to the authenticated client. Isolated client SHA-256: `51BE30D3E55231D300ABD7BD41E046690DD43EE6A4DAF5F9CD9AF4A92EFF03E2`. Active `Client/7670/WYD.exe` was not launched because its hash/provenance differs and remains unverified.
- [x] Update the existing traceability, smoke, progress, backlog, and root handoff in place with the authenticated and visual results, corrected client fixture, and active-binary limitation.
