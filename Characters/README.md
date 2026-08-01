# Characters Directory

Named fictional people are the **unit of load**. Archetypes A–F are voice/matrix templates only ([Main.md](../Framework/Main.md), [voices.md](../Framework/Mechanics/voices.md)).

## Card vs log vs live snapshot

| File | Role |
|:---|:---|
| `Characters/[slug].md` | **Identity / build sheet** — voice, wound/gift, build-default weights, history anchors |
| `Characters/[slug]_log.yaml` | **Durable runtime** — snapshot + history; overrides card Focus/weights/somatic when present |
| Live snapshot | **Per-tick** psychosomatic state (`Framework/Schemas/psychosomatic_state.json`) from the Cognitive Pipeline |
| [`_log_template.yaml`](./_log_template.yaml) | Schema scaffold for new durable logs |

Do **not** write movement deltas onto the card. Durable evolution commits go to the log (see [CognitivePipeline.md](../Framework/CognitivePipeline.md) §8 and [Character_Change_Log.md](../Framework/Character_Change_Log.md)). Scene close goes to [Continuity_Ledger.md](../Framework/Continuity_Ledger.md).

## Card format

Character cards are **pure YAML** (`.md` extension for tooling compatibility):

- Entire card is a single YAML document between `---` fences
- Structured fields: identity, psyche matrix, `transformation_weights` (**build defaults**), `depth_of_knowledge`, `voice`, `history_anchors`, `scene_seeds`
- One-line load protocol after the closing `---` (overlays `_log.yaml` snapshot when present)
- No duplicate markdown tables — the YAML is the identity source of truth

## Drafting flow

1. Author names on-scene characters.
2. Load `Characters/[slug].md` (or a pasted card).
3. Overlay `Characters/[slug]_log.yaml` when present.
4. Run [CognitivePipeline.md](../Framework/CognitivePipeline.md) with realm_data + trigger → live snapshot + 4-channel vector.
5. [Main.md](../Framework/Main.md) + [Rules_Index.md](../Framework/Rules_Index.md) render manuscript prose.
6. On approval: commit durable fields to log + Continuity_Ledger (not the card).

## Files

- [`_template.md`](./_template.md) — **public** card scaffold (CC BY-SA 4.0)
- [`_log_template.yaml`](./_log_template.yaml) — **public** log schema (CC BY-SA 4.0)
- [`README.md`](./README.md) — this file (public format docs)

See [LICENSE.md](../LICENSE.md) §3 for the carve-out. Downstream projects should start from `_template.md` + `_log_template.yaml` only.

## Optional live test (not the product core)

Drafting uses this folder + Framework. For a **chat stress-test** of a card (or private sessions), paste [`Simulator/CharacterRuntime.md`](../Simulator/CharacterRuntime.md). Portable **Character Pack** (CARD + MEMORY) mirrors card + `_log.yaml`.

## Adding a novel character

1. Copy `_template.md` → `Characters/[slug].md` (or run [character_builder_prompt.md](../Framework/Prompts/character_builder_prompt.md))
2. Copy `_log_template.yaml` → `Characters/[slug]_log.yaml`; seed snapshot from the card (`as_of: build`); leave `history: []`
3. Add a Snapshot row in [Character_Change_Log.md](../Framework/Character_Change_Log.md)
4. Fill card YAML: **age**, **canon_adult**, physical, cultural_bias, psyche matrix from Main + realm_data.yaml
5. Voice: nearest A–F base under `voice:`, then override with idiolect on the card
6. Drafting: load Main + Rules_Index + realm_data.yaml + card + log (+ Continuity_Ledger)
