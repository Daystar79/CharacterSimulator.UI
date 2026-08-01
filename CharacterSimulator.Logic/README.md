# CharacterSimulator.Logic — Ported Bookkeeping & Host Services

This library houses deterministic host logic and bookkeeping rules for `CharacterSimulator.UI` in C#.

## Architecture Principles

- **C# owns**: File I/O, schemas, numeric clamping, safety gates, durable log commits, parsing/validation, slash commands, prompt assembly, output hygiene linting, and somatic catalog lookups.
- **LLM owns**: Cognitive Pipeline "mind" and RP prose rendering. No procedural math replaces psychological roleplay.
- **UI projects (GUI/TUI)** stay thin by wiring directly to services in this package.

## New Sub-namespaces and Services

### 1. `State/` — Psychosomatic Models & Validation
- **`PsychosomaticModels.cs`**: Typed snapshot models (`AutonomicState`, `AffectiveState`, `SubconsciousBias`, `RelationalVector`, `PriorityArbitration`, `OutputVector`).
- **`ScaleClamps.cs`**: Utility enforcing `[0, 100]` integer range bounds for all numeric scales.
- **`PsychosomaticStateValidator.cs`**: Validates JSON/snapshots against `psychosomatic_state.json` rules, clamps ranges, and applies live state to runtime `Character` instances.

### 2. `Logs/` — Durable Log Persistence & Transformation
- **`DurableLogModels.cs`**: Typed representation of `Characters/[slug]_log.yaml`.
- **`DurableLogStore.cs`**: Load/save durable YAML logs. Enforces **log overlay precedence** over identity card defaults (focus, latent weights, bias_strength, skills, memories, relational baselines, default somatic). Never writes runtime evolution back into identity cards.
- **`PressureApplicator.cs`**: Port of Midlayer `logs_io.apply_pressure`. Applies deterministic strength deltas (`low`: 0, `medium`: 5, `high`: 10, `extreme`: 15), permanence mapping, and history tracking.
- **`CommitService.cs`**: Manages in-memory pressure updates per turn, and serializes durable logs to disk on session close or manual `/save`.

### 3. `Safety/` — Code-Enforced Safety Gates
- **`AgeGate.cs`**: Hard gate blocking intimate/HEAT paths if `canon_adult == false` or `age < 18`.
- **`AdultAuth.cs`**: Requires dual authorization (user adult attestation + character card age eligibility).
- **`HardBanFilter.cs`**: Scans rendered dialogue for character `hard_bans` and redacts/flags violations.

### 4. `Hygiene/` — Output Hygiene Linter
- **`SystemLeakLinter.cs`**: Port of RP system-leak patterns (Realm references, Focus Lock, Debt Ledger, therapy jargon, OOC lookups, AI safety tone leaks). Redacts and flags critical leaks in agent output.

### 5. `Somatics/` — Somatic & Realm Catalog
- **`RealmDataCatalog.cs`**: Loads `realm_data.yaml` (Realms I–X). Provides zone lookups, micro/moderate/macro/release somatic cues, and vocal behaviors for prompts and `/state` UI displays.

### 6. Data Assets (`Data/`)
- `CharacterSimulator.Logic/Data/psychosomatic_state.json`
- `CharacterSimulator.Logic/Data/realm_data.yaml`

## Testing

Unit tests are located in `CharacterSimulator.Logic.Tests`:
- Scale clamping and JSON schema validator tests
- `apply_pressure` strength deltas and permanence tests
- Log overlay precedence tests
- Age gate enforcement tests
- System leak linter regex detection tests
