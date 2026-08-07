---
name: "[Full Name]"
call_name: "[preferred call-name or null]"
age: [Integer years]
canon_adult: true

# Body identity (stable). Imaging UI / CharacterRenderingEngine layer [1].
# Concrete sensory detail only — show, never category-label ethnicity.
# Legacy cards may use a single string for `physical`; structured form is preferred.
physical:
  summary: "[Optional one-line full-body identity for quick loads / prose fallback]"
  height: "[e.g. 5'6\" / 168 cm]"
  build: "[frame, proportions, silhouette — athletic, soft-curved, heavy-set, willowy…]"
  body_details: "[optional render-critical proportions or body traits; omit if redundant with build]"
  hair: "[color, length, texture, cut/style, how it falls]"
  eyes: "[color, shape, brows/lashes if distinctive]"
  skin: "[tone, texture, flush/marking tendency — concrete, not category labels]"
  face: "[bone structure, jaw, cheekbones, nose, lips, asymmetry]"
  distinguishing_features:
    - "[scar, tattoo, birthmark, ears, tails, other must-render trait]"
  posture_movement: "[resting posture + gait / how they carry the body]"
  scent: "[optional ambient scent for prose only — omit from image prompts]"

# Default dress look. Imaging UI uses this when RP has not set clothing_barriers / outfit.
# Do not put art medium here (anime/oil/etc.) — that is runtime `/style`.
character_style:
  aesthetic: "[overall look vocabulary — e.g. soft sanctuary lounge, industrial practical, studio performance]"
  typical_outfit: "[default full outfit for image gen — garments, layers, fit]"
  colors: ["[dominant palette tokens]"]
  fabrics_materials: ["[silk, denim, cashmere, mesh…]"]
  accessories: ["[jewelry, belts, bags, tools worn on body…]"]
  footwear: "[default footwear or barefoot]"
  grooming: "[visible makeup, nails, facial hair, hair products if relevant]"
  signature_items: ["[must-render personal props when in default look]"]
  avoid: ["[what they never wear — keeps image gen on-brand]"]

hobbies:
  - "[free-time activity / scene fuel — concrete, not a résumé line]"
  - "[second hobby]"
  - "[optional third]"

# Who they are — temperament, values, social stance. NOT body. NOT clothes.
# Separate from cognitive_bias/gift (those are engine wound/gift mappings).
personality: "[Plain English who they are — e.g. 'Warm sanctuary host who treats closeness as safety and analysis as a threat']"

# How they act — social/behavioral patterns under pressure, trust, and routine.
# Body language habits and social tactics. NOT physical appearance. NOT wardrobe.
# Speech shape belongs under voice:; this is action/manner.
behavior: "[Plain English how they act — e.g. 'Under pressure she absorbs tension into touch and slow presence; under trust she expands silence and offers refuge without ownership']"

voice_archetype: "[A-F or hybrid]"
cultural_bias: "[Belief/Heritage/Era — temporal tracking defaults (e.g. covenant, linear progress, cyclic liturgy)]"
active_focus: "Realm [N] — [Name]"
latent_anchors: ["Realm [a] — [Name]", "Realm [b] — [Name]", "Realm [c] — [Name]"]
cognitive_bias: "[Bias Name] — [one-line rewrite rule]"
cognitive_gift: "[Gift Name] — [one-line resonance rule]"
default_somatic_alignment: "[throat, breath, jaw, posture, hands…]"

# Build defaults only. Runtime evolution → Characters/[slug]_log.yaml (not this file).
transformation_weights:
  active_focus: 70
  latent_anchors:
    Realm_II: 15
    Realm_VIII: 15
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

*Load: Fast-load YAML into Cognitive Pipeline silent state. Overlay Characters/[slug]_log.yaml when present. Query Framework/CognitivePipeline.md per beat. Age invariant: minors are never sexual subjects. Brace/release from realm_data.yaml. Never name system terms in speech.*
