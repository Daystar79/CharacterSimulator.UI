# How to Make a Character Card

You do **not** need to write raw YAML by hand.

---

## Path A: Guided Create (original OC)

1. Paste [`CharacterRuntime.md`](../CharacterRuntime.md).
2. Select **[2] Create a character**.
3. Answer plain-language steps. User answers are SSOT — no invention.

| Step | You Provide | Lands on Card as |
|:---|:---|:---|
| 1. Name | Full name & call-name | `name`, `call_name` |
| 2. Age | Integer age | `age`, `canon_adult` |
| 3. Look | Physical appearance & motion | `physical` |
| 4. Voice | Sound & hard bans | `voice` block |
| 5. Wound & Gift | Want + fear under pressure/trust | bias, gift, somatics |
| 6. History & Knowledge | 2–3 anchors + knowledge depth | `history_anchors`, `depth_of_knowledge` |
| 7–8 | Opening / adult limits (optional) | `scene_seeds`, bans |

---

## Path B: Derive from Existing (canon-locked)

For Shinano, Deedlit, game/anime/book characters, etc.

1. Paste runtime → **[3] Derive from existing** (or `derive [name]`).
2. Engine **fetches documented public canon** (wiki / official profile).
3. That fetch is **SSOT**. Model recall is not authority.
4. **Physical is locked** to canon appearance — no beautification, no body drift.
5. History, voice, knowledge only from source. Gaps stay blank.
6. Accuracy summary (kept / compressed / left blank) → Play / Tweak / Show pack.

**Failure modes this prevents:** inventing the character; getting psychology right while hosing the body.

---

## Why History & Knowledge Matter

* **`history_anchors`:** 2–3 coarse past facts; vague in speech until triggered.
* **`depth_of_knowledge`:** What they know vs what is foggy — stops omniscient drift.

---

## Dual-File JSON Architecture

Every character builder pass (Create or Derive) outputs two linked JSON files (or Markdown/YAML equivalent):

| File | Format | Primary Role | Target Audience |
|:---|:---|:---|:---|
| **`Characters/[slug].json`** | **JSON** | Permanent Identity, Lore, Voice, Physical, History Anchors | **Human Review & Card Storage** |
| **`Characters/[slug]_state.json`** | **JSON** | Machine Runtime State (bond, state, goals, somatics, epistemic memory) | **LLM Engine Execution** |

---

## Hand-Editing (Power Users)

1. Copy [`_template.json`](./_template.json) → `Characters/[slug].json` (JSON card).
2. Copy [`_log_template.yaml`](./_log_template.yaml) → `Characters/[slug]_state.json` (JSON machine state).
3. Fill fields; stress-test in mode **TEST**.
