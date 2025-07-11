The plan below translates the high‑level three‑phase roadmap into **twelve concrete coding sprints**. Each sprint is a time‑boxed, self‑contained slice of work that delivers reviewable code in the **thenovelist** .NET 8 solution. Tasks are ordered to respect dependencies so that all metadata is captured early, propagated correctly, and guarded all the way to the finished manuscript.

---

## Phase 1 – Continuity Recovery (4 Sprints)

### Sprint 1.1 — Metadata Foundation

* **Add `ProjectMetadata` record** to `src/Novelist.OutlineBuilder/Models` with required properties (`Premise`, `Genre`, `Audience`, `StylePreset`). Use `[JsonRequired]` attributes so missing fields throw during deserialization ([Microsoft Learn][1]).
* **Extend `outline.schema.v1.json`**: add a top‑level `"metadata"` object with the same required fields (update version to 1.1).
* **Modify `Program.cs` (CLI)** to parse a `.project.json` file or CLI flags into `ProjectMetadata` and inject it via DI when constructing outline services ([Microsoft Learn][2], [Microsoft Learn][3]).
* **Functional verification**: run `dotnet run -- init thedoor.project.json`; confirm the generated outline JSON contains a populated `metadata` object.

---

### Sprint 1.2 — Character‑First Pipeline

* **Re‑order service calls** in `OutlineBuilderService` so `CharactersOutlinerService` executes before `ArcDefinerService`—aligning with agile “definition‑before‑use” practices ([easyagile.com][4]).
* **Amend `ArcDefinerService` prompt builder** to include a bullet list of canonical character names/IDs and forbid new names with an explicit rule line.
* **Functional verification**: regenerate a sample outline; inspect that act summaries reuse canonical names.

---

### Sprint 1.3 — Canonical Links

* **Extend models** `Act`, `Beat` with `IReadOnlyList<Guid> CharacterIds` and update serialization code.
* **Update JSON schema** to enforce `characterIds` array (non‑empty) on every beat for validation.
* **Patch `BeatsExpanderService`** to fill the new field by mapping names in beat text to IDs before writing JSON.
* **Functional verification**: run outline expansion and ensure each beat’s `characterIds` count > 0.

---

### Sprint 1.4 — Early Consistency Gate

* **Create `ContinuityValidator`** (Console app step) that scans the outline after Phase 1 completes and fails on:

  * Unknown names in acts/beats.
  * Beats referencing subplot IDs that do not exist.
* **Wire validator** into `Program.cs` right after outline build; exit ≠ 0 on first failure (simple for now).
* **Functional verification**: deliberately break a character name; ensure CLI stops with a clear message.

