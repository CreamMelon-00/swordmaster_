# Current enemy artwork

Current native source: `Expansion/Enemy_AllAttacks3_Hurt.aseprite` (118 cels, 12 tags).
The enemy now has three motions for each attack type and one hurt pose, selected and displayed by the arena runtime. See `Expansion/EXPANSION.md` for behavior, validation, and file details.

Root-level 45-cel source and its report are retained as the earlier base release. The expansion preserves those cels and adds 73 new ones. Preview generation used the built-in image tool, followed by Aseprite pixel processing and rigid weapon-axis correction; prompts are in `Expansion/PROMPTS.md`.
