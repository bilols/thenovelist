# Novelist – Three‑Phase Technical Change Plan  
*(excerpted from the full redesign blueprint; dev‑chat reseed guide omitted)*

---

## Phase 1 – Continuity Recovery

### Objectives
* Eliminate character/name drift (Ethan ↔ Ian, Mr Porter ↔ Henry).  
* Store premise + metadata once; propagate to every phase.  
* Wire characters into arcs and beats via canonical IDs.  
* Add basic validation tests to catch cross‑phase mismatches.

### Key Tasks by Sub‑system

| Sub‑system | Critical fixes | Impacted files |
|------------|----------------|----------------|
| **Project / Premise** | Introduce `ProjectMetadata` (premise, genre, style) and embed it in outline header. | `Program.cs`, `outline.schema.v1.json`, models. |
| **Characters** | Move Character Generation *before* Story Arc in CLI workflow; output canonical list. | `Program.cs` ordering, `CharacterGeneratorService.cs`. |
| **Story Arc** | Update prompt to reference canonical characters; forbid inventing new names. | `ArcDefinerService.cs`. |
| **Schema Link** | Add `characterIds` (array) to `acts` and `beats`. | `tools/outline.schema.v1.json`, outline parsers. |
| **Validation** | Extend `validate_artifacts.py` to flag beats/acts using unknown names or missing subplots. | `tests/validate_artifacts.py`. |

---

## Phase 2 – Prompt Scaffolding & Guard‑rails

### Objectives
* Preserve full context (characters, beats, subplots) in every generation prompt.  
* Prevent omission of required beats / subplot events.  
* Provide checklist‑style prompts and automated retries for missing content.

### Key Tasks by Sub‑system

| Sub‑system | High‑priority scaffolds | Implementation notes |
|------------|------------------------|----------------------|
| **Sub‑plots** | Add `SubPlotGeneratorService`; store `subplots[]` top‑level with IDs. | New service + schema update. |
| **Beats** | Switch to iterative beat generation; each prompt includes previous beat, character tags, required subplot line. | Modify beat generator; add `characters`, `subplotId` fields to each beat. |
| **Chapters** | Create `ChapterContextBuilder` to collate premise snippet, act summary, beats, subplot lines, character cheat‑sheet. | New helper; update `StructureOutlinerService`. |
| **Draft prompts** | Replace “Remaining beats: N” with explicit beat text (or IDs + summaries). Include per‑character trait reminders. | `DraftBuilderService.BuildPiecePrompt`. |
| **Guard‑rails** | Extend duplicate detector; add `BeatCoverageValidator` to reject pieces that skip outlined beats. | `DuplicateDetector.cs`, new validator. |

---

## Phase 3 – Fidelity Enforcement & Polishing

### Objectives
* Guarantee final manuscript covers every outline beat & subplot.  
* Provide automated post‑draft QA and cost metrics.  
* Expose user‑tunable settings for retry counts, piece size, POV enforcement.

### Key Tasks by Sub‑system

| Sub‑system | Improvements | Files / tools |
|------------|--------------|---------------|
| **Drafting Engine** | Generate chapter‑by‑chapter; inject outline context per chapter; add post‑draft QA comparing beats ↔ prose. | `DraftBuilderService.cs`, new `DraftValidator.cs`. |
| **Post‑draft Validation** | Cross‑check every beat/subplot against manuscript; output QA report. | `DraftValidator.cs`, CLI flag `--review-draft`. |
| **Cost/metrics UI** | Extend `CostLogger` to aggregate by phase; display totals after run. | `CostLogger.cs`, CLI summary. |
| **Config** | Add `novelist.config.schema.json` for piece size, retries, POV rules. | Config loader + docs. |
| **Documentation** | Update README; add sample `.project.json`; describe new CLI flags. | `README.md`, `docs/`. |

---

### Implementation Order

1. **Branch `phase‑1‑continuity`** → implement reordering, schema links, basic validation → regenerate sample outline.  
2. **Branch `phase‑2‑scaffolds`** → add subplot service, beat prompts, guard‑rails → verify outline completeness.  
3. **Branch `phase‑3‑fidelity`** → upgrade drafting engine, QA validator, config UX → ship beta.

Each phase should compile & run end‑to‑end before proceeding to the next.