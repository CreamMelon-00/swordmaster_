# Current enemy artwork

Current native source: `Reactions/Enemy_AllAttacks3_Reactions2.aseprite` (120 cels, 14 tags).
The enemy has three motions per attack type, two guard poses and two hurt poses. Each incoming hit or successful guard independently selects a reaction. The original hurt pose was also corrected to match the idle body's scale and proportions.

See `Reactions/REACTIONS.md` for behavior, validation and native-source details. New-pose generation used built-in image_gen followed by Aseprite editing; exact prompts are in `Reactions/PROMPTS.md`.

The root 45-cel source and `Expansion/Enemy_AllAttacks3_Hurt.aseprite` (118 cels) are retained as earlier versions. Attack design and integration history remain in `Expansion/EXPANSION.md`.