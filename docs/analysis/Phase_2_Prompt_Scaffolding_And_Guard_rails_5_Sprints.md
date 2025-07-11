## Phase 2 – Prompt Scaffolding & Guard‑rails (5 Sprints)

### Sprint 2.1 — Subplot Service

* **Add `SubPlotGeneratorService`** with prompt template: *“Create exactly N subplot threads (IDs S1…SN) linked to the premise and characters.”*
* **Augment schema**: top‑level `"subplots"` collection plus `subplotIds` on acts.
* **Functional verification**: generate outline with `subPlotDepth:2`; confirm `subplots` and act links exist.

---

### Sprint 2.2 — Iterative Beat Generator

* **Refactor `BeatsExpanderService`** to loop through acts and call LLM once per beat (or small batch) passing: previous beat text, target characters, and subplot ID.
* **Add new fields** `Characters`, `SubplotId`, `Intent` inside each beat JSON node for richer context.
* **Functional verification**: ensure sequential beats reference previous context and carry correct `SubplotId`.

---

### Sprint 2.3 — `ChapterContextBuilder`

* **Create utility** in `src/Novelist.DraftBuilder` that composes:

  * Premise snippet (`metadata.Premise`, truncated 50 words).
  * Act summary.
  * Three beat synopses for the chapter.
  * Character cheat sheet (`Name — Traits`).
  * Active subplot lines.
* **Embed context** object into chapter node of outline for easy retrieval.
* **Functional verification**: inspect updated outline; each chapter should show the assembled context block.

---

### Sprint 2.4 — Prompt Checklist Upgrade

* **Rewrite `DraftBuilderService.BuildPiecePrompt`** to:

  * Remove “Remaining beats: N”.
  * Insert bullet list `• Beat 1 – ... • Beat 2 – ...`.
  * Append *“All listed beats must appear in this piece”* guard line referencing AI‑safety checklists ([arXiv][5], [arXiv][6]).
* **Include per‑character reminders** (name + key trait) just under the beats list.
* **Functional verification**: generate Chapter 1 piece 1 prompt; confirm new checklist present.

---

### Sprint 2.5 — Guard‑rail Enforcers

* **Extend `DuplicateDetector`** to work at paragraph granularity and raise retry if similarity > 85 % Jaccard (threshold informed by industry best practice) ([Medium][7]).
* **Implement `BeatCoverageValidator`**: after each chapter piece is drafted, scan text and mark beats as “covered” using fuzzy match; retry piece (max 3) if beats remain uncovered.
* **Functional verification**: skip a beat intentionally by editing prompt; ensure validator forces a retry.