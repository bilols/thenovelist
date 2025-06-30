## Outstanding Items (need answers before coding)

1. Confirm `outline.schema.v2.json` draft structure.  
2. Directory where author preset JSON files live (`/author_presets`)?  
3. Max retries per LLM phase (default **3**?).  
4. Name prefix for progress phases in events (exact strings matter for UI).  
5. Licence concerns for storing author presets (they appear to be self‑written JSON; confirm).
---

## Open Questions Summary
| ID | Question |
|----|----------|
| **Q1** | Confirm schema changes (prologue, epilogue, stylePreset) as proposed. |
| **Q2** | Where should `author_presets` folder sit (root, `/data`, etc.)? |
| **Q3** | Desired default **maxRetries** per LLM pass (integer). |
| **Q4** | Exact `Phase` labels you want surfaced in `OutlineProgress`. |
| **Q5** | Any license / attribution constraints on famous‑author preset content? |
---
