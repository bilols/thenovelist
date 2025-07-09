## What to include in a Dev‑Session Reseed File

This will need to be updated eventually to have full *accurate* details (it's a little janked). 

| Section                            | Content                                                                                                                                                                           | Why it’s needed                     |
| ---------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------- |
| **Project snapshot (≤ 200 words)** | One‑paragraph description of The Novelist: purpose, tech stack (.NET 8, Spectre.Console CLI, OutlineBuilder & DraftBuilder DLLs), key data flow (Project → Outline → Draft).      | Gives immediate orientation.        |
| **Repo topology**                  | Bulleted tree of critical folders/files *(only top level and important classes, e.g. `src/Novelist.OutlineBuilder/*`, `DraftBuilderService.cs`, `tools/outline.schema.v1.json`)*. | Lets AI locate code quickly.        |
| **Known pain‑points**              | 3‑5 bullets summarising the context‑loss audit (Ethan vs Ian, beats not carried, etc.).                                                                                           | Reminds AI of main bugs to fix.     |
| **Three‑phase plan outline**       | Very concise table: Phase 1 = Continuity, Phase 2 = Prompt/guardrails, Phase 3 = Fidelity/polish.                                                                                 | Sets immediate roadmap.             |
| **Active branch & task**           | Sentence like: *“Current branch: phase‑1‑continuity; next task: move CharacterGen before ArcGen and update CLI order.”*                                                           | Tells AI what to work on first.     |
| **Key file URLs**                  | Direct raw links to 5‑10 files most likely to be edited in the next task (use entries from `CurrentRepo_TheNovelist_URLs.txt`).                                                   | AI can fetch latest code instantly. |
| **Rules for code drops**           | “Full file replacements only, no fragments; match repo style; keep .NET 8; update schema + tests.”                                                                                | Reinforces working agreement.       |

### Size guide

* Aim for **\~500‑700 words total** so it fits in one prompt.
* Keep code URLs on separate lines for easy copy‑click.

---

## Example skeleton (fill with current details)

```markdown
**The Novelist – Dev Primer**

Purpose: AI‑assisted C#/.NET 8 tool that converts a project.json premise into a full novel draft (OutlineBuilder → DraftBuilder).  Uses Spectre.Console CLI; JSON schemas in /tools.

Repo layout (key parts)
- src/
  - Novelist.OutlineBuilder/
    - PremiseExpanderService.cs
    - ArcDefinerService.cs
    - CharacterGeneratorService.cs   <-- to be moved earlier
  - Novelist.DraftBuilder/
    - DraftBuilderService.cs
    - PrologueEpilogueBuilderService.cs
- tools/outline.schema.v1.json
- docs/analysis/Novelist_Context_Loss_Audit.md
- docs/analysis/Novelist_Project_Recovery_Redesign_Plan.md

Main problems (from audit)
1. Character names drift (Ethan ↔ Ian) because characters generated after arcs.
2. Beats & sub‑plots not passed into draft prompts; context lost.
3. DraftBuilder only sends counts, not beat text → missing plot points.

Three‑phase recovery (high level)
| Phase | Goal | Key actions |
|-------|------|-------------|
| 1 | Continuity | Reorder CharacterGen; add character IDs in acts/beats; schema update. |
| 2 | Guardrails | Prompt scaffolds w/ beat & subplot checklists; Duplicate + BeatCoverage validators. |
| 3 | Fidelity | Chapter‑by‑chapter drafting with outline injection; post‑draft QA. |

Current branch/task: **phase‑1‑continuity** – refactor CLI `Program.cs` order; update ArcDefiner prompt to use canonical characters.

Important source URLs  
https://raw.githubusercontent.com/bilols/thenovelist/main/src/Novelist.OutlineBuilder/CharacterGeneratorService.cs  
https://raw.githubusercontent.com/bilols/thenovelist/main/src/Novelist.OutlineBuilder/ArcDefinerService.cs  
https://raw.githubusercontent.com/bilols/thenovelist/main/src/Novelist.Cli/Program.cs  
https://raw.githubusercontent.com/bilols/thenovelist/main/tools/outline.schema.v1.json  

Rules: full‑file replacements only; maintain .NET 8; update tests in validate_artifacts.py when schema changes.
```

Paste that primer into a new chat and the assistant can immediately:

1. Load each raw URL to view code.
2. Understand current architecture and pain points.
3. Start implementing the next item in the roadmap without needing prior chat history.

---

### Automate generation

Add a small CLI flag, e.g.:

```bash
dotnet run --project src/Novelist.Cli -- generate-dev-primer --branch phase-1-continuity
```

A simple `DevPrimerWriter.cs` can pull:

* `ProjectMetadata`
* Current branch name (`git rev-parse --abbrev-ref HEAD`)
* Next task stub (read from TODO file)
* A fixed list of critical URLs (maintained in repo)

…and write `dev_primer.md` for easy copy‑paste.

This gives you a **one‑command reseed sheet** whenever you start a fresh ChatGPT/o3‑pro session.
