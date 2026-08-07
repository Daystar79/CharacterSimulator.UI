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
| 3. Look | Body for imaging (height, build, hair, eyes, skin, face, marks, motion) | `physical` block |
| 4. Style | How they dress, accessories, palette, signature items | `character_style` block |
| 5. Personality | Who they are — temperament, values, social stance | `personality` |
| 6. Behavior | How they act under pressure / trust / routine | `behavior` |
| 7. Hobbies | Free-time activities / scene fuel | `hobbies` list |
| 8. Voice | Sound & hard bans | `voice` block |
| 9. Wound & Gift | Want + fear mapped to engine bias/gift | bias, gift, somatics |
| 10. History & Knowledge | 2–3 anchors + knowledge depth | `history_anchors`, `depth_of_knowledge` |
| 11–12 | Opening / adult limits (optional) | `scene_seeds`, bans |

---

## Path B: Derive from Existing (canon-locked)

For Shinano, Deedlit, game/anime/book characters, etc.

1. Paste runtime → **[3] Derive from existing** (or `derive [name]`).
2. Engine **fetches documented public canon** (wiki / official profile).
3. That fetch is **SSOT**. Model recall is not authority.
4. **Physical is locked** to canon appearance — no beautification, no body drift.
5. **Style** locks to documented outfits/accessories when the source provides them; otherwise leave fields blank rather than invent fashion.
6. History, voice, knowledge, hobbies only from source. Gaps stay blank.
7. Accuracy summary (kept / compressed / left blank) → Play / Tweak / Show pack.

**Failure modes this prevents:** inventing the character; getting psychology right while hosing the body.

---

## Keep these separate (do not merge)

| Field | Is | Is not |
|:---|:---|:---|
| **`personality`** | Who they are (temperament, values, social stance) | Body, clothes, or speech syntax |
| **`behavior`** | How they act (pressure / trust / routine manners) | Physical appearance or wardrobe |
| **`physical`** | Body identity for imaging & prose | Personality, habits, or fashion |
| **`character_style`** | Default dress / accessories | Art medium or temperament |
| **`voice`** | How they sound and what they never say | How they move or dress |
| **`cognitive_bias` / `gift`** | Engine wound/gift rewrite rules | Plain-English personality blurb |

UI and loaders must show **personality**, **behavior**, and **physical** as separate panels — never one combined “description” blob.

---

## Imaging fields (`physical` + `character_style`)

These two blocks feed the imaging UI / [`CharacterRenderingEngine`](../Images/CharacterRenderingEngine.md):

| Block | Role in a still |
|:---|:---|
| **`physical`** | Identity layer — body that must stay stable across frames |
| **`character_style`** | Default clothing layer when RP has not set an outfit |

**`physical` keys (structured form preferred):** `summary`, `height`, `build`, `body_details`, `hair`, `eyes`, `skin`, `face`, `distinguishing_features`, `posture_movement`, `scent` (prose only — omit from image prompts).

**`character_style` keys:** `aesthetic`, `typical_outfit`, `colors`, `fabrics_materials`, `accessories`, `footwear`, `grooming`, `signature_items`, `avoid`.

**Not on the card:** art medium (anime/oil/photoreal) — that is runtime `/style`, not dress style.

Legacy cards may still use a single string for `physical`; tools should accept both forms.

---

## Why History, Knowledge & Hobbies Matter

* **`history_anchors`:** 2–3 coarse past facts; vague in speech until triggered.
* **`depth_of_knowledge`:** What they know vs what is foggy — stops omniscient drift.
* **`hobbies`:** Concrete free-time activities for scene seeds, props, and small talk — not a résumé.

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
