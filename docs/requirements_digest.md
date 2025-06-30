Digest reviewed

# Requirements Digest v1.1  (2025‑06‑30)

## Global

* Follow the **Universal Rules** in *Project‑Level Guidance*.
* Runtime target: **.NET 8 LTS / C# 13**.
* Unit tests: **xUnit** + **FluentAssertions** (single test project **Novelist.Tests**).
* Logging: **Serilog** wired through `Microsoft.Extensions.Logging` abstractions.
* Command‑line host: **Spectre.Console ≥ 1.4** (used by the optional smoke‑test harness).
* JSON serialization: **Newtonsoft.Json ≥ 14.0**.
* JSON‑schema validation: **JsonSchema.Net**.
* **No legacy Python code reuse** — all features are re‑implemented in C#; previous algorithms may be referenced conceptually only.

---

## Folder & File Naming Rules

| Artifact           | Path pattern                                                             | Notes                                                |
| ------------------ | ------------------------------------------------------------------------ | ---------------------------------------------------- |
| Generated outlines | `projects/<slug-project‑timestamp>/outlines/<slug-title‑timestamp>.json` | Timestamp format **`yyyyMMdd-HHmmss`** (local time). |

---

## Schemas & Versioning Convention

| Schema file              | Status      | Key additions in v1                                                |
| ------------------------ | ----------- | ------------------------------------------------------------------ |
| `outline.schema.v1.json` | **Current** | `schemaVersion`, `famousAuthor`, `characters[]`, `outlineProgress` |
| `project.schema.v1.json` | **Current** | Governs all incoming `project.json` files                          |

* **Minor**, backward‑compatible tweaks increment the **`schemaVersion`** integer inside each v1 schema.
* **Breaking** updates create `outline.schema.v2.json`, `project.schema.v2.json`, etc., plus a migration script (TBD).

---

## OutlineProgress Enumeration

`Init → PremiseExpanded → ArcDefined → CharactersOutlined → ChaptersSketched → BeatsDetailed → Finalized`

---

## LLM Model Catalogue

* Supported model IDs and their numeric backend mapping reside in **`/config/llmModels.json`**.
* The catalogue loads at runtime; the system MUST fall back gracefully if a requested model is unavailable.

---

## Outline Builder Subsystem (headless)

* **Input**: `project.json` (validated against `project.schema.v1.json`).

* **Output**: `outline.json` (conforms to `outline.schema.v1.json`).

* Optional sections: **prologue**, **epilogue**

  * Each ≈ ½ the average chapter word‑count.
  * Their words **do not** count toward the overall novel word goal.

* Famous‑author “style presets” (e.g., *Stephen King*) must be accepted and surfaced in prompt design.

* **Public API**:

  ```csharp
  Task<Outline> BuildAsync(
      Project project,
      IProgress<OutlineProgress> progress,
      CancellationToken ct);
  ```

* **Progress events**: at minimum one per `OutlineProgress` phase and one per *retry attempt* inside any LLM call.

* **Cancellation**: standard `CancellationToken`.

* **Architecture**: class‑library DLL; no UI and no web server.

* **OpenAI access**: via `ILLMClient` interface (default implementation uses `OpenAI.Client` SDK).

  * API key injection strategy is caller‑defined; recommended default reads the `OPENAI_API_KEY` environment variable.

---

## Milestones (deliver in order)

0. **Skeleton solution** – compiles, empty classes.
1. **Validation layer** – schemas + POCOs + unit tests.
2. **Stub LLM client** – deterministic outline generation.
3. **Real LLM integration** – prompt templates, Serilog instrumentation.
4. **(Optional) CLI smoke‑test harness** built with Spectre.Console.

---

## Testing Assets & Coverage

* Supply **three** sample `project.json` variants:

  1. 10‑chapter, 80 k‑word novel, “Stephen King” preset, **prologue only**.
  2. 20‑chapter, 50 k‑word novel, **no** prologue/epilogue.
  3. 15‑chapter, 90 k‑word novel, **prologue + epilogue**, alternate author preset.
* The **Novelist.Tests** project MUST verify:

  * Schema compliance for both schemas.
  * Correct generation of storage paths per naming rules.
  * CLI smoke‑run (if the harness is included).
  * Valid, ordered transitions through the `OutlineProgress` enum.

---

*End of Requirements Digest v1.1*
