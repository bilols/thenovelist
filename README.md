
# The Novelist (.NET 8)

***Currently broken and being updated***

**The Novelist** is a C#/.NET 8 command‑line tool that transforms a short
project description (premise, genre, target length) into:

1. A structured, validated outline (acts → beats → chapters)  
2. A full first‑draft manuscript in Markdown  
3. Cost metrics for every LLM call

It consists of two main libraries:

| Library | Responsibility |
|---------|----------------|
| **Novelist.OutlineBuilder** | Expands premise → characters → story arc → sub‑plots → beats → chapter structure (JSON). |
| **Novelist.DraftBuilder**   | Converts outline into prose (multi‑pass, chunked prompts) + cost logging. |

## Repository Layout

```

src/
├─ Novelist.OutlineBuilder/
├─ Novelist.DraftBuilder/
├─ Novelist.Cli/            ← Spectre.Console entry‑point
tools/                       ← JSON schemas
docs/
└─ analysis/                ← audit & redesign reports

````

## Current Roadmap (2025‑Q3)

| Phase | Goal | Status |
|-------|------|--------|
| **1 – Continuity** | Character‑first ordering, schema links, validation for name/ID drift. | **in‑progress** (`phase‑1‑continuity` branch) |
| **2 – Guardrails** | Prompt scaffolds with beat/sub‑plot checklists, duplicate + beat‑coverage validators. | planned |
| **3 – Fidelity** | Chapter‑by‑chapter drafting with outline injection, post‑draft QA, reseed automation. | planned |

See `docs/analysis/Novelist_Project_Recovery_Redesign_Plan.md` for full details.

## Build & Run (quick start)

```bash
# restore & build
dotnet build

# run full example (uses ./src/samples/thedoor.project.json)
run_novelist_live.bat
````

## Dev‑Session Reseed

***UNDER CONSTRUCTION***

If a new ChatGPT/o3‑pro session is opened, you can quickly re‑prime it:

```bash
dotnet run --project src/Novelist.Cli -- generate-dev-primer \
           --branch phase-1-continuity \
           --output docs/dev_primer.md
```

Copy‑paste the contents of `docs/dev_primer.md` into the new chat.
The primer contains:

* 200‑word project snapshot
* Folder tree & critical source URLs
* Top 3 audit findings
* Three‑phase roadmap table
* Current task pointer (e.g. “Move CharacterGen before ArcGen”)

## License

MIT © 2025 Bill Olson & contributors
