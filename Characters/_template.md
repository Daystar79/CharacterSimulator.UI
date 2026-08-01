---
name: "[Full Name]"
call_name: "[preferred call-name or null]"
age: [Integer years]
canon_adult: true
physical: "[Coloration, features, bone structure, movement — show, never category-label]"
voice_archetype: "[A-F or hybrid]"
cultural_bias: "[Belief/Heritage/Era — temporal tracking defaults (e.g. covenant, linear progress, cyclic liturgy)]"
active_focus: "Realm [N] — [Name]"
latent_anchors: ["Realm [a] — [Name]", "Realm [b] — [Name]", "Realm [c] — [Name]"]
cognitive_bias: "[Bias Name] — [one-line rewrite rule]"
cognitive_gift: "[Gift Name] — [one-line resonance rule]"
default_somatic_alignment: "[throat, breath, jaw, posture, hands…]"
somatic_zones:
  - "Face/Eyes: [zone-specific tell]"
  - "Throat/Neck: [zone-specific tell]"
  - "Chest/Breath: [zone-specific tell]"
  - "Hands/Arms: [zone-specific tell]"
  - "Spine/Posture: [zone-specific tell]"
  - "Feet/Staging: [zone-specific tell]"

# Build defaults only. Runtime evolution → Characters/[slug]_log.yaml (not this file).
transformation_weights:
  active_focus: 70
  latent_anchors:
    II: 15
    VIII: 15
  bias_strength: 60
  somatic_flexibility: 40

depth_of_knowledge:
  general: "[broad understanding]"
  esoteric: "[ritual/occult knowledge level]"
  personal: "[memory clarity vs. blanks]"

voice:
  baseline: "[register summary — e.g. 'Breathy, melodic, childlike lilt; vulnerable warmth']"
  syntactical_engine: "[concrete sentence structures and patterns — e.g. 'Fragmented clauses; breathy upward inflection; heavy oh/well/you know; short 3-5 word bursts']"
  conversational_stance: "[dominant | yielding | evasive | counter-querying | directive | buffering]"
  verbal_defense: "[verbal action under pressure — e.g. 'insulates with technical jargon', 'deflects with questions', 'over-explains', 'silences self', 'smothers with care']"
  generative_stance: "[verbal action under safety/trust — e.g. 'unhurried, expansive explanations', 'invites collaborative discovery', 'grounds with direct, gentle clarity']"
  hard_bans: ["[what this character never says — e.g. 'Intellectual jargon', 'cold precision']"]
  signature_tics: ["[repeated words/gestures — e.g. 'Darling...', breathy laughter, hair-tuck]"]
  relational_verbal_shifts:
    "[Target Character Name]": "[specific verbal posture towards this character — e.g. 'short directive bursts, cuts off emotional preambles']"

history_anchors:
  - "[Anchor 1 — coarse, scene-useful; memories stay vague unless triggered]"
  - "[Anchor 2]"
  - "[Anchor 3]"

# Optional — also record bonds in Characters/Relations.md
# relationships:
#   - other: "[Name]"
#     dynamic: "[bond type]"
#     notes: "[how Focus/Bias warps them]"

scene_seeds:
  - "[Place + pressure + object]"
  - "[Alternate seed]"
---

*Load: Fast Load YAML. Copy matrix, voice, somatic, history_anchors, depth_of_knowledge, adult-gate to silent state. Overlay Characters/[slug]_log.yaml (snapshot, memories, skills) when present. Epistemic: card + log + session only. Dual-aspect: under pressure apply verbal_defense (Wound); under safety/trust apply generative_stance (Gift). 18+ OFF until gates. Run Focus brace/release from Framework/Psychology/realm_data.yaml. Never name system terms in speech.*
