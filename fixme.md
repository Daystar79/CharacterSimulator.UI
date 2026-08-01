# FIXME — Known bugs & residual gaps

Tracked leftovers from the deterministic Logic port (Gemini) and surgical hardening pass (Mistral).  
Do not treat these as product features; they are correctness / safety / hygiene fixes.

---

## High

### [ ] Age-ineligible characters still get normal prompts
- **Where:** `CharacterSimulator.Logic/PromptBuilder.cs` (`BuildFullPrompt` / identity rules)
- **Problem:** `AgeGate.IsAdultEligible` is never consulted when building LLM prompts. Ineligible cards (`canon_adult: false` or age &lt; 18) receive the same instructions as adult-eligible ones.
- **Fix:** If `!AgeGate.IsAdultEligible(character)`, append a hard OOC constraint: no intimate/sexual content; non-intimate scene only; never sexualize this character.
- **Also consider:** When adult framing is added later, require `AdultAuth.IsAdultPathAuthorized` for HEAT-capable paths (user attestation × character eligibility).

### [ ] Structured live-state extraction is unreliable
- **Where:** `PsychosomaticStateValidator.ExtractStateJson`, `TurnManager.ParseResponse`
- **Problem:**
  - `ExtractStateJson` uses a non-nested pattern `(\{[^{}]*\})` — real JSON with nested objects fails.
  - Tests for extract were skipped with a note that patterns don’t work as intended.
  - Fallback regex still tends to stop at the first `]`.
- **Fix:** Implement balanced-brace scan after `[State:`, and/or support `<state>…</state>` / fenced `json` blocks properly; add unit tests. Or remove dead extract path and document that only `[Somatic]` / bond markers are supported until prompts emit a simpler contract.
- **Related:** After sanitize, `ApplyToCharacter` re-validates strictly (e.g. requires `character_id`). Partial LLM payloads often fail entirely — either document that or apply safe subset only.

---

## Medium

### [ ] TUI does not handle `/adult`
- **Where:** `CharacterSimulator.TUI/TerminalUi.cs`
- **Problem:** `PlayerCommandKind.Adult` exists and GUI handles it; TUI has no equivalent wiring.
- **Fix:** Parse slash commands consistently; implement `/adult on|off` (and help text) in TUI if TUI remains a ship surface.

### [ ] In-memory durable log can start disconnected from disk
- **Where:** `TurnManager.ApplyPressureInMemory`
- **Problem:** If `character.DurableLog` is null, a fresh empty log is created without loading an existing `*_log.yaml` first. History/path can be orphaned until session commit if loader overlay was skipped.
- **Fix:** Prefer `DurableLogStore.LoadLog` via expected path when log is null; only create empty shape if file missing.

### [ ] Session commit always uses strength `low` on close
- **Where:** `CommitService.CommitSession`
- **Problem:** End-of-session flush maps character state then applies `low` pressure (no history row unless override). In-memory medium+ history from the session should still persist when saving the log; verify flush writes full in-memory `history[]` and snapshot, not only a low-pressure no-op path that drops prior in-memory rows.
- **Fix:** On session end / explicit save, serialize current `DurableLog` (including in-memory history) without re-applying pressure that clears or skips needed fields; use pressure only for real pressure events.

---

## Low

### [ ] Realm Roman-numeral lookup can mis-resolve
- **Where:** `Somatics/RealmDataCatalog.GetRealm`
- **Problem:** `key.Contains("I")` / `"II"` etc. is order-sensitive; strings like `VIII` can match shorter tokens (e.g. `I` or `V`) incorrectly depending on iteration order.
- **Fix:** Match longest Roman token first, or use word-boundary / regex with ordered alternation (`X|IX|VIII|…|I`).

### [ ] README slightly stale on commit triggers
- **Where:** `CharacterSimulator.Logic/README.md`
- **Problem:** Still implies medium+ pressure always commits to disk; turn path is in-memory only, disk on session end / `/save`.
- **Fix:** Document memory vs flush split.

### [x] Root-level legacy C# clutter / monoproject solution break
- **Where:** repo root (`AgyClient.cs`, `Character.cs`, `Program.cs`, …) + `CharacterSimulator.UI.csproj` / `CharacterSimulator.Testing.csproj`
- **Problem:** Old single-project SDK csproj at repo root recursively compiled **all** nested GUI/Logic/TUI/Tests sources → duplicate assembly attributes, missing Avalonia/xunit refs, solution build failure. VS Code `build` task used that solution.
- **Fix (done):** Removed root monoprojects and legacy root `.cs`; `CharacterSimulator.UI.sln` now references Logic + GUI + TUI + Logic.Tests only. Also `CharacterSimulator.slnx` for modern tooling.

### [ ] Linter false positives (optional)
- **Where:** `Hygiene/SystemLeakLinter.cs`
- **Problem:** Bare tokens like `Insulation` / `Dissolution` can redact normal English.
- **Fix:** Narrow critical-only patterns for RP host if false positives show up in playtests.

---

## Verification checklist (when fixing)

- [ ] `dotnet test CharacterSimulator.Logic.Tests`
- [ ] `dotnet build` GUI + TUI
- [ ] Age-ineligible prompt contains non-intimate constraint (string/assert or manual)
- [ ] `/adult on|off` works in every shipped UI
- [ ] Autoplay does not write `*_log.yaml` every medium turn; end/save does
- [ ] `Data/realm_data.yaml` present under app output directory
