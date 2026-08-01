# TODO — New features & product roadmap

Work that is **not** bugfix (see `fixme.md`). Ordered for a friend-testable build first.

Architecture reminder:

```text
UI  →  Roleplay module (host)  →  Cognitive engine
```

New features below belong in the **roleplay host + thin UI**, not in the cognitive engine.  
Midlayer (book writing) only needs the cognitive engine and is out of scope here.

### Storage decision (updated)

**Runtime user data is SQLite, not files.**

| On disk as files | In SQLite |
|---|---|
| Shared character **templates** (`Characters/*.md`, json) | Profiles, PIN meta, DOB (encrypted), prefs |
| Shipped assets (`Data/realm_data.yaml`, schemas) | Sessions, turns/history, mode/scene/cast |
| Optional human **exports** (md/json for debugging) | Character progress per profile (bond, bias, log fields, history events) |
| | Roster / last-played indexes |

- **Source of truth** for multi-user progress = database(s).  
- Prefer **one encrypted DB per profile** (open on unlock, close on lock/switch) so PIN isolation is physical, not “remember the WHERE clause.”  
- YAML `*_log.yaml` / `session_*.json` paths are **legacy**; new code should not grow them. Optional one-way import later.

### Preferred implementer

- **Primary for this roadmap: Gemini Pro** (large context — read Logic + GUI + fixme + this file in one shot).  
- Keep prompts seam-scoped (host/DB only; do not reimplement cognitive engine).  
- Mistral/others: fine for narrow follow-ups; Gemini for the multi-feature SQLite + profile slice.

---

## P0 — Friend-test MVP: multi-user on one PC + SQLite

Single machine, multiple people, sequential use. Soft privacy + clear “who is playing.”

### [ ] SQLite host data layer
- [ ] Add SQLite dependency appropriate for .NET 10 (e.g. `Microsoft.Data.Sqlite`; encryption strategy chosen below)
- [ ] `schema_version` + simple migration runner (forward-only is enough for v1)
- [ ] Repository APIs in Logic (not GUI): profiles, sessions, turns, character progress
- [ ] Replace / wrap `SessionService` and durable progress currently aimed at files
- [ ] Transactional save (session + turns + progress in one commit where sensible)
- [ ] DB location under app data or `./Profiles/<id>/` — document path

**Suggested schema (v1, adjust names as needed):**

```text
profiles
  id, display_name, pin_salt, kdf_params, pin_verifier, created_at, last_opened_at

-- either encrypted columns or whole-DB key; pick one approach and stick to it
profile_secrets / vault
  profile_id, dob (cipher or sealed), prefs_json (cipher), ...

sessions
  id, profile_id, title, scene, genre, mode, status, started_at, updated_at

session_participants
  session_id, character_slug, slot_order

session_turns
  id, session_id, turn_index, speaker, dialogue, somatic_json, bond, meta_json, created_at

character_progress
  profile_id, character_slug, bias_strength, active_focus, bias_state,
  snapshot_json, updated_at
  -- snapshot_json can hold former durable-log fields without over-normalizing

character_history
  id, profile_id, character_slug, movement_id, pressure, delta, permanence, notes, created_at
```

### [ ] Profile system
- [ ] Create / list / select active profile
- [ ] Switch profile: flush dirty session → close DB / wipe key → picker (one active profile at a time)
- [ ] Show active profile name prominently (window title / header)
- [ ] Public profile list must not require PIN (display names only)

### [ ] Player identity & age for adult gate
- [ ] On create: display name + date of birth (month / day / year)
- [ ] Store DOB only in encrypted profile secret space; never plain column in a shared open DB without encryption
- [ ] Derive age at runtime from DOB (local date); document timezone assumption
- [ ] Adult path formula:
  - profile age ≥ 18
  - **and** user adult attestation (when required)
  - **and** character `canon_adult` + character age ≥ 18
- [ ] Under-18 profile: adult path permanently off (no override)
- [ ] **Never** send full DOB to the LLM or into conversation transcripts / turn rows
- [ ] Attestation UX: `/adult` and/or first-run dialog for adult profiles; persist attestation on profile if desired

### [ ] PIN + encrypt at rest (SQLite-aware)
- [ ] On create: PIN + confirm PIN
- [ ] Loud warning: forgot PIN = data unrecoverable (unless recovery codes in P1)
- [ ] Key derivation: PBKDF2 or Argon2id + per-profile salt
- [ ] **Preferred v1:** per-profile database file, encrypted at rest
  - Option A: SQLCipher (or equivalent) with PIN-derived key  
  - Option B: AES-GCM seal of the entire `.db` file when locked; open only while unlocked  
- [ ] Wrong PIN fails closed; optional attempt delay / lockout
- [ ] Decrypt/open only while profile unlocked; lock on switch/exit
- [ ] Same sealed unit later becomes the cloud sync artifact (P3)

### [ ] Character templates vs progress
- [ ] Shared preset cards remain **files** under `Characters/` (read-only templates)
- [ ] All progress/history/sessions live in **SQLite for that profile**
- [ ] Roster query: characters this profile has interacted with (from `character_progress` / sessions)
- [ ] Do not write evolution back into template cards

### [ ] Session save / resume (DB)
- [ ] Session = scene, genre, cast, mode (auto/guided), status, turn list
- [ ] Create session on setup start; append turns as host events fire
- [ ] Resume: list sessions for profile → load cast + turns into UI/host
- [ ] Explicit save + auto-flush on profile switch / clean exit (transaction)
- [ ] Multiple sessions per profile supported from day one (table design); UI can show “continue last” first

### [ ] Wire existing host services
- [ ] `TurnManager` / commit path: character progress + history → SQLite, not mid-turn YAML thrash
- [ ] `CommitService` / `DurableLogStore`: either become SQLite-backed or deprecated behind a repository facade
- [ ] In-memory pressure still OK; **flush target is DB**
- [ ] Logger conversation export may still write a human-readable file under profile export dir (optional); not SSOT

### [ ] GUI (required for friends)
- [ ] Cold start → profile picker (not bare main window)
- [ ] Create profile wizard: name, DOB, PIN
- [ ] Unlock with PIN → open profile DB
- [ ] Switch profile from menu
- [ ] Session list / continue last / new session
- [ ] Existing setup + play flow only after unlock

### [ ] TUI (optional for MVP)
- [ ] Profile pick / create / unlock in terminal, or document “GUI only for multi-user v1”

### [ ] Migration policy for existing file data
- [ ] Friend builds: **start clean on SQLite** (recommended)
- [ ] Optional later: one-shot import of old `*_log.yaml` / `session_*.json` into a default profile DB
- [ ] Document in `FRIENDS_TEST.md` / README

### [ ] Tests & ship bar
- [ ] Unit tests: schema migrate, create profile, wrong PIN, age from DOB, session round-trip, progress isolation across two profiles (two DBs or strict profile_id)
- [ ] Manual: create → play turns → quit → unlock → session + progress intact
- [ ] Manual: second profile cannot read first without PIN
- [ ] Manual: under-18 profile never gets adult prompt path
- [ ] `dotnet test` + `dotnet build CharacterSimulator.UI.sln` green

---

## P1 — Friend-test polish

### [ ] Recovery code (optional but reduces support pain)
- [ ] One-time recovery code at profile create (wraps data key / DB key)
- [ ] “Forgot PIN” flow using recovery code only
- [ ] Still no server-side recovery

### [ ] PIN change
- [ ] Unlock with old PIN → re-key DB / re-seal → atomic replace

### [ ] Clear “who is playing” UX
- [ ] Profile name always visible during play
- [ ] Confirm on switch if unsaved session dirty

### [ ] First-run / empty-state copy for testers
- [ ] `FRIENDS_TEST.md`: create profile, PIN warning, SQLite location, `/adult`, how to report bugs

### [ ] Version stamp for builds
- [ ] Assembly / informational version visible in UI or `/status`
- [ ] (Later) GitHub release check — see P2

### [ ] Optional export tools
- [ ] Export session transcript to `.md` for sharing (from DB, not SSOT)
- [ ] Export character progress snapshot JSON for debugging

---

## P2 — Distribution & updates

### [ ] GitHub Releases packaging
- [ ] Publish GUI (and optionally TUI) artifacts for friend installs
- [ ] Simple version check: `GET .../releases/latest`, compare semver, open releases page
- [ ] No silent auto-update in v1; HTTPS + fail-open if offline

### [ ] Update check service (local)
- [ ] Config: repo slug, check on startup (optional), last-check cache
- [ ] User-Agent header for GitHub API

---

## P3 — Cloud saves (encrypted DB blob, later)

Do **not** block friend MVP on this.

### [ ] Client-side only encryption for cloud
- [ ] Upload/download the **same per-profile sealed SQLite unit** as local
- [ ] Server stores ciphertext only; never receives PIN
- [ ] Account/device token = locker number; PIN = locker key
- [ ] Conflict policy: last-write-wins or version vector on the blob
- [ ] Stronger PIN / passphrase guidance when cloud enabled (offline brute force on stolen blob)
- [ ] PIN change re-keys and re-uploads

---

## P4 — Roleplay host enhancements (after multi-user stable)

### [ ] Stronger response contract
- [ ] Stable markers or small JSON for live snapshot the host can always parse
- [ ] Align with `PsychosomaticStateValidator` without fragile regex
- [ ] Persist useful live fields into `session_turns.meta_json` / progress as needed

### [ ] HEAT / intimacy presentation controls
- [ ] Host depiction settings (SFW / fade / explicit) gated by adult formula
- [ ] Never rewrite character want/refusal; only presentation
- [ ] Store depiction pref on profile in DB

### [ ] Session quality-of-life
- [ ] Named save slots / session titles
- [ ] Delete/archive sessions
- [ ] Backup: copy encrypted profile DB to USB

### [ ] Cognitive engine integration (host-side only)
- [ ] Optional load of pipeline / rules snippets into system prompt from known paths
- [ ] Keep psyche math out of C# (engine remains prompt/spec + deterministic edges already in Logic)

---

## Explicit non-goals (for now)

- File trees as SSOT for sessions/logs (`Profiles/**/sessions/*.json`, mid-session YAML)  
- Online accounts as the encryption root  
- Server-side decrypt / “forgot password email”  
- Midlayer manuscript ledger inside this UI  
- Reimplementing Cognitive Pipeline psychology in C#  
- Multi-profile concurrent play on one process (one unlocked profile at a time)  

---

## Suggested implementation order (Gemini-friendly)

Large-context pass can do 1–5 in one coherent PR if scoped tightly; still land in this order:

1. Clear remaining **`fixme.md` highs** (age prompt gate, state extract or drop, TUI `/adult` if shipping TUI)  
2. **SQLite schema + migrations + repositories** in Logic (plaintext DB OK for first compile spike)  
3. **Profiles + DOB adult math** on top of repos  
4. **PIN + per-profile DB encryption/seal**  
5. **Sessions + turns + character progress** wired from `TurnManager` / GUI save paths  
6. **GUI** picker / create / unlock / switch / continue session  
7. Tests + friend notes  
8. Recovery code / PIN change polish  
9. GitHub release / version check  
10. Cloud sealed-DB sync  

### Prompt hints for Gemini

- Read: `todo.md`, `fixme.md`, `CharacterSimulator.Logic/**`, `CharacterSimulator.GUI/MainWindow.axaml.cs`, `SessionService.cs`, `Logs/*`, `Safety/*`  
- Do **not** rewrite cognitive pipeline or Midlayer  
- Prefer repository interfaces so GUI stays thin  
- Keep `Characters/` templates as files  
- Solution entry: `CharacterSimulator.UI.sln` (Logic + GUI + TUI + Tests) — do **not** resurrect a root monoproject `.csproj`  

Update this file as items complete (`[x]`).
