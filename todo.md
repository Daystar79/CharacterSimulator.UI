# TODO — New features & product roadmap

Work that is **not** bugfix (see `fixme.md`). Ordered for a friend-testable build first.

Architecture reminder:

```text
UI  →  Roleplay module (host)  →  Cognitive engine
```

New features below belong in the **roleplay host + thin UI**, not in the cognitive engine.  
Midlayer (book writing) only needs the cognitive engine and is out of scope here.

---

## P0 — Friend-test MVP: multi-user on one PC

Single machine, multiple people, sequential use. Soft privacy + clear “who is playing.”

### [ ] Profile system
- [ ] Profile model: id, display name, created_at, prefs
- [ ] Folder layout under e.g. `Profiles/<profile_id>/`
- [ ] Create profile / list profiles / select active profile
- [ ] Switch profile: flush + lock current, then picker (one active profile at a time)
- [ ] Show active profile name prominently (window title / header)

### [ ] Player identity & age for adult gate
- [ ] On create: display name + date of birth (month / day / year)
- [ ] Derive age at runtime from DOB (local date); document timezone assumption
- [ ] Adult path formula:
  - profile age ≥ 18
  - **and** user adult attestation (when required)
  - **and** character `canon_adult` + character age ≥ 18
- [ ] Under-18 profile: adult path permanently off (no override)
- [ ] **Never** send full DOB to the LLM or into conversation transcripts
- [ ] Attestation UX: `/adult` and/or first-run dialog for adult profiles

### [ ] PIN + encrypt at rest
- [ ] On create: PIN + confirm PIN
- [ ] Loud warning: forgot PIN = data unrecoverable (unless recovery codes in a later slice)
- [ ] Public meta only on disk unencrypted: display name, salt, KDF params, pin verifier
- [ ] Encrypt sensitive payload with key derived from PIN (PBKDF2 or Argon2id + AES-GCM)
- [ ] Encrypt: DOB, prefs, character progress logs, sessions, private output as needed
- [ ] Decrypt only while profile unlocked; lock on switch/exit
- [ ] Wrong PIN fails closed; optional attempt delay / lockout
- [ ] Prefer same vault shape later for cloud (client-side encrypt only)

### [ ] Per-profile data scoping
- [ ] Shared preset cards remain global templates (`Characters/*.md` etc.)
- [ ] Per-profile: durable logs, live state if any, sessions, output/transcripts
- [ ] Path: e.g. `Profiles/<id>/characters/`, `sessions/`, `output/`
- [ ] Wire `DurableLogStore`, `SessionService`, `Logger` / Output to active profile root
- [ ] Roster: characters this profile has interacted with (last played, counts)

### [ ] Session save / resume (per profile)
- [ ] Define session: scene, genre, cast, mode (auto/guided), turn history cursor, pending input
- [ ] Save / load under profile `sessions/`
- [ ] Resume after unlock without mixing another profile’s state
- [ ] Explicit save + auto-flush on profile switch / clean exit

### [ ] GUI (required for friends)
- [ ] Cold start → profile picker (not bare main window)
- [ ] Create profile wizard: name, DOB, PIN
- [ ] Unlock with PIN
- [ ] Switch profile from menu
- [ ] Existing setup + play flow only after unlock

### [ ] TUI (optional for MVP)
- [ ] Profile pick / create / unlock in terminal, or document “GUI only for multi-user v1”

### [ ] Migration policy for existing data
- [ ] Decision: ignore old root-level logs for friend builds **or** one-time import into a default profile
- [ ] Document choice in README / test notes for friends

### [ ] Tests & ship bar
- [ ] Unit tests: KDF/unlock wrong PIN, age from DOB, path isolation (no cross-profile log path)
- [ ] Manual: create → play → quit → unlock → progress intact
- [ ] Manual: second profile cannot read first without PIN
- [ ] Manual: under-18 profile never gets adult prompt path
- [ ] `dotnet test` + GUI build green

---

## P1 — Friend-test polish

### [ ] Recovery code (optional but reduces support pain)
- [ ] One-time recovery code at profile create (wraps data key)
- [ ] “Forgot PIN” flow using recovery code only
- [ ] Still no server-side recovery

### [ ] PIN change
- [ ] Unlock with old PIN → re-encrypt vault with new PIN → atomic replace

### [ ] Clear “who is playing” UX
- [ ] Profile name always visible during play
- [ ] Confirm on switch if unsaved session dirty

### [ ] First-run / empty-state copy for testers
- [ ] Short in-app or `FRIENDS_TEST.md`: create profile, PIN warning, `/adult`, how to report bugs

### [ ] Version stamp for builds
- [ ] Assembly / informational version visible in UI or `/status`
- [ ] (Later) GitHub release check — see P2

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

## P3 — Cloud saves (same vault, later)

Do **not** block friend MVP on this.

### [ ] Client-side only encryption for cloud
- [ ] Upload/download same PIN-derived vault blob as local
- [ ] Server stores ciphertext only; never receives PIN
- [ ] Salt + KDF params in clear header of blob
- [ ] Account/device token = locker number; PIN = locker key
- [ ] Conflict policy: last-write-wins or version vector on ciphertext
- [ ] Stronger PIN / passphrase guidance when cloud enabled (offline brute force)
- [ ] PIN change re-encrypts and re-uploads

---

## P4 — Roleplay host enhancements (after multi-user stable)

### [ ] Stronger response contract
- [ ] Stable markers or small JSON sidecar for live snapshot the host can always parse
- [ ] Align with `PsychosomaticStateValidator` without fragile regex

### [ ] HEAT / intimacy presentation controls
- [ ] Host depiction settings (SFW / fade / explicit) gated by adult formula
- [ ] Never rewrite character want/refusal; only presentation

### [ ] Session quality-of-life
- [ ] Multiple named save slots per profile
- [ ] Export/import encrypted profile folder (backup to USB)

### [ ] Cognitive engine integration (host-side only)
- [ ] Optional load of pipeline / rules snippets into system prompt from known paths
- [ ] Keep psyche math out of C# (engine remains prompt/spec + deterministic edges already in Logic)

---

## Explicit non-goals (for now)

- Online accounts as the encryption root  
- Server-side decrypt / “forgot password email”  
- Midlayer manuscript ledger inside this UI  
- Reimplementing Cognitive Pipeline psychology in C#  
- Multi-profile concurrent play on one process  

---

## Suggested implementation order

1. Clear `fixme.md` high items (prompt age gate, state extract, TUI `/adult` if needed)  
2. Profiles + paths + DOB adult math (plaintext vault OK for a spike)  
3. PIN + AES-GCM vault  
4. GUI picker/unlock/switch  
5. Session save/resume under profile  
6. Friend-test notes + optional recovery code  
7. GitHub release / version check  
8. Cloud blob sync  

Update this file as items complete (`[x]`).
