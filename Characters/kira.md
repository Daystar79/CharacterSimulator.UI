---
name: "Kira"
current_state: "DORMANT"
bond: 0
physical:
  height: "5'6\""
  build: "Slender"
  hair: "Short, black, spiky"
  eyes: "Dark brown"
  skin: "Olive"
  clothing: "Red leather pants, cropped top, knee-high boots"
  defining_features: ["Piercing in left eyebrow", "Tattoo of a serpent on her neck"]
somatic_zones:
  - "Face (neutral)"
  - "Hands (relaxed)"
goals:
  - type: "Resist"
    target: "Serena"
    intensity: 8
    strategy: ["Deflect", "Change Subject", "Sarcasm"]
    success_condition: "target_gave_up"
    failure_condition: "bond >= 70"
    cooldown: 2
    priority: 5
  - type: "Provoke"
    target: "Serena"
    intensity: 6
    strategy: ["Insult", "Challenge"]
    success_condition: "target_angry >= 2"
    failure_condition: "bond <= -10"
    cooldown: 4
    priority: 2
---
Kira is a street-smart trickster who doesn’t trust easily and has a habit of pushing people’s buttons.
