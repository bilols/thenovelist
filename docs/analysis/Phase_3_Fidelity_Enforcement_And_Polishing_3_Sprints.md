# Phase 3 – Fidelity Enforcement & Polishing (3 Sprints)

## Sprint 3.1 — Chapter‑Scoped Drafting

* **Refactor `DraftBuilderService`** so `BuildManuscript` iterates chapters; for each:

  * Load `ChapterContextBuilder` output.
  * Generate pieces sequentially (respecting configured piece size).
  * Inject previous‑chapter recap in first piece prompt for smooth narrative flow ([teamhood.com][8]).
* **Functional verification**: run full draft; confirm piece tags and chapter order remain intact.

---

## Sprint 3.2 — `DraftValidator` & Review Mode

* **Create `DraftValidator`** that parses final markdown and matches every beat & subplot ID from outline to prose.
* **Add CLI flag `--review-draft`** to invoke validator post‑draft; output a CSV of omissions for manual fix.
* **Functional verification**: remove a subplot sentence; validator should list it as missing.

---

## Sprint 3.3 — Metrics & Config

* **Augment `CostLogger`** to aggregate stats per phase (outline vs draft) and print a summary table on completion.
* **Introduce `novelist.config.json`** (validated by `project.schema.v1.json`) with knobs:

  * `PieceTargetWords`, `MaxRetries`, `EnforcePOV`, `GuardrailStrictness`.
* **Load config** at startup and inject via DI to all services ([Microsoft Learn][2], [Microsoft Learn][3]).
* **Functional verification**: set `MaxRetries:1`; provoke duplicate paragraph; system should quit after a single retry.

---

[1]: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/required-properties?utm_source=chatgpt.com "Require properties for deserialization - .NET | Microsoft Learn"
[2]: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-usage?utm_source=chatgpt.com "Tutorial: Use dependency injection in .NET - Learn Microsoft"
[3]: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection?utm_source=chatgpt.com "Dependency injection - .NET | Microsoft Learn"
[4]: https://www.easyagile.com/blog/agile-sprint-planning?utm_source=chatgpt.com "The Ultimate Agile Sprint Planning Guide [2024]"
[5]: https://arxiv.org/html/2403.18958v1?utm_source=chatgpt.com "A State-of-the-practice Release-readiness Checklist for Generative ..."
[6]: https://arxiv.org/html/2408.02205v1?utm_source=chatgpt.com "Towards AI-Safety-by-Design: A Taxonomy of Runtime Guardrails in ..."
[7]: https://medium.com/freelancers-hub/i-tested-5-ai-detectors-heres-my-review-about-what-s-the-best-tool-for-2025-35a58eac86c5?utm_source=chatgpt.com "I Tested 30+ AI Detectors. These 9 are Best to Identify Generated Text."
[8]: https://teamhood.com/project-management/sprint-in-project-management/?utm_source=chatgpt.com "Scrum Sprint Project Management: A Detailed Agile Workflow Guide"
