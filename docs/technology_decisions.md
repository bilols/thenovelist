## Technology Desicions
These are the technology decisions that have been made so far.

- **Language/Runtime:** C# 12 / .NET 8 class library (`OutlineBuilder.Core`).
- **OpenAI SDK:** `OpenAI.Client` (wrapped by `ILLMClient`).
- **Config:** Inject API key; default env var fallback.
- **Build Artifacts:** `dotnet pack` → local NuGet.
- **Logging:** ILogger abstractions only; caller decides sinks.
- **Unit Tests:** xUnit.
- **Package Management:** NuGet; use `Directory.Packages.props` for central versions.
---

# Updated decisions on 06/30/2025 after discussion, some superceding the previous:

| Area                   | Decision                                                                                                        |
| ---------------------- | --------------------------------------------------------------------------------------------------------------- |
| **Runtime / language** | .NET 8 (C# 13)                                                                                                  |
| **JSON stack**         | **Newtonsoft.Json** for all serialization + schema‑driven work                                                  |
| **Logging**            | **Serilog** (with `ILogger` abstraction for host apps)                                                          |
| **DI**                 | Microsoft default container                                                                                     |
| **Schema**             | `outline.schema.v1.json` is canonical; each outline carries an internal `"schemaVersion"` for minor revs        |
| **Storage**            | Filesystem only – path pattern: `./projects/<slug‑project‑name‑timestamp>/outlines/<slug‑title‑timestamp>.json` |
| **Namespaces**         | `Novelist.OutlineBuilder` (root)                                                                                |
| **Update semantics**   | Always write a **new version**; never mutate in place                                                           |
| **Async**              | All public operations are `async`/`CancellationToken` aware                                                     |
| **Multi‑pass logic**   | Re‑implemented in C#; token math concept retained                                                               |
| **Tests**              | Single consolidated **Novelist.Tests** (xUnit, JsonSchema.Net)                                                  |
| **CLI**                | `novelist outline create …` / `validate …`; smoke‑tested via `dotnet test`                                      |
| **Help / exit codes**  | Follow common .NET CLI conventions for now                                                                      |
