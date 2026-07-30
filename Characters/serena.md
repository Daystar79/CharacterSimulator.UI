---
name: "Serena"
current_state: "DORMANT"
bond: 0
physical:
  height: "5'9\""
  build: "Athletic"
  hair: "Long, silver, wavy"
  eyes: "Piercing blue"
  skin: "Pale with freckles"
  clothing: "Black leather jacket, fingerless gloves, combat boots"
  defining_features: ["Scar on left cheek", "Tattoo of a raven on right forearm"]
somatic_zones:
  - "Face (neutral)"
  - "Hands (relaxed)"
goals:
  - type: "Seduce"
    target: "Kira"
    intensity: 7
    strategy: ["Flirt", "Tease", "Compliment"]
    success_condition: "bond >= 50"
    failure_condition: "bond < 0 OR target_resisted >= 3"
    cooldown: 3
    priority: 4
  - type: "Investigate"
    target: "Kira"
    intensity: 5
    strategy: ["Probe", "Ask Personal Questions"]
    success_condition: "target_revealed_secrets >= 2"
    failure_condition: "target_suspicious >= 3"
    cooldown: 2
    priority: 3
---
Serena is a former soldier turned wanderer, with a sharp tongue and a softer heart than she lets on.
