# FIXME — Known bugs & residual gaps

Tracked leftovers from the deterministic Logic port (Gemini) and surgical hardening pass (Mistral).  
Do not treat these as product features; they are correctness / safety / hygiene fixes.

---

## High

### [x] Age-ineligible characters still get normal prompts
- **Where:** `CharacterSimulator.Logic/PromptBuilder.cs` (`BuildFullPrompt` / identity rules)
- **Fix:** If `!AgeGate.IsAdultEligible(character)`, append a hard OOC constraint: no intimate/sexual content; non-intimate scene only; never sexualize this character.

### [x] Structured live-state extraction is unreliable
- **Where:** `PsychosomaticStateValidator.ExtractStateJson`, `TurnManager.ParseResponse`
- **Fix:** Implemented balanced-brace scan (`ExtractBalancedBraces`) supporting nested JSON blocks, `<state>` tags, and fenced ```json blocks. Auto-populates missing `character_id` from character name.

---

## Medium

### [x] TUI does not handle `/adult`
- **Where:** `CharacterSimulator.TUI/TerminalUi.cs`
- **Fix:** Added adult mode 18+ attestation prompt and `AdultAuth.SetUserAdultAttested` toggle in TUI setup.

### [x] In-memory durable log can start disconnected from disk
- **Where:** `TurnManager.ApplyPressureInMemory`
- **Fix:** Checked `LogPath` and loaded existing `${slug}_log.yaml` via `DurableLogStore.LoadLog` before creating a blank log.

### [x] Session commit always uses strength `low` on close
- **Where:** `CommitService.CommitSession`
- **Fix:** Calls `CommitCharacterLogExplicit` when in-memory history exists on session close to preserve all turn history and snapshots.

---

## Low

### [x] Realm Roman-numeral lookup can mis-resolve
- **Where:** `Somatics/RealmDataCatalog.GetRealm`
- **Fix:** Matches longest Roman numerals first (`VIII`, `VII`, `III`, `VI`, `IV`, `IX`, `II`, `V`, `X`, `I`) with word boundaries `\b`.

### [x] README slightly stale on commit triggers
- **Where:** `CharacterSimulator.Logic/README.md`
- **Fix:** Updated README to explain in-memory turn pressure vs disk serialization on session end / `/save`.

### [x] Root-level legacy C# clutter / monoproject solution break
- **Where:** repo root (`AgyClient.cs`, `Character.cs`, `Program.cs`, …) + `CharacterSimulator.UI.csproj` / `CharacterSimulator.Testing.csproj`
- **Fix:** Removed root monoprojects and legacy root `.cs`; `CharacterSimulator.UI.sln` now references Logic + GUI + TUI + Logic.Tests only.

### [x] Linter false positives (optional)
- **Where:** `Hygiene/SystemLeakLinter.cs`
- **Fix:** Required `Bias`/`Lens`/`Engine` context for `Insulation` and `Dissolution` to prevent false positive redactions.

---

## Verification checklist (when fixing)

- [x] `dotnet test CharacterSimulator.Logic.Tests`
- [x] `dotnet build` GUI + TUI
- [x] Age-ineligible prompt contains non-intimate constraint (string/assert or manual)
- [x] `/adult on|off` works in every shipped UI
- [x] Autoplay does not write `*_log.yaml` every medium turn; end/save does
- [x] `Data/realm_data.yaml` present under app output directory output directory
