
# The Novelist – Requirements Digest  v1.2 (2025‑07‑01)

> **IMPORTANT**  
> 1. Follow the *Universal Rules* below for every code or content drop.  
> 2. Deliver **full‑file replacements only.** Partial snippets break the build pipeline.

---

## 0. Universal Rules

| # | Rule |
|---|------|
| 1 | Always re‑read the latest **requirements_digest.md** before responding to ensure no conflict or omission. |
| 2 | **Full‑file delivery only.** Each reply that supplies source must contain complete, ready‑to‑paste files. |
| 3 | Touch only files directly related to the requested feature / fix. All others must remain byte‑for‑byte identical. |
| 4 | Start every technical reply with the line **“Digest reviewed”** to confirm compliance. |
| 5 | Ask clarifying questions when doubt exists—never guess. |

---

## 1. Schemas

| Schema | Status | Notes |
|--------|--------|-------|
| **project.schema.v1.json** | **FROZEN**. Only additive, backward‑compatible bug‑fixes until v2. |
| **outline.schema.v1.json** | **FROZEN** (same rule). |
| Any breaking change → introduce `outline.schema.v2.json` plus a migration helper. |

### 1.1 Recent additive fields
* `targetAudience` (array, required).  
* Optional chapter‑level `themes` (string[]).  
* `outlineProgress` enum gains **StructureOutlined** member between *CharactersOutlined* and *BeatsDetailed*.

---

## 2. Outline Progress Phases

```

Init
PremiseExpanded
ArcDefined
CharactersOutlined
StructureOutlined   ← NEW (Scenes/Beats pass)
BeatsDetailed       (future)
Finalized

```

Each pass must:
1. Validate incoming phase.  
2. Generate strictly‑valid JSON output.  
3. Increment `header.schemaVersion`.  
4. Persist **in place** (caller/gitrepo handles history).

---

## 3. Tech Stack

| Area | Choice |
|------|--------|
| Runtime | .NET 8 / C# 13 |
| Logging | Serilog |
| JSON Schema | JsonSchema.Net |
| Source gen | Scriban templates where helpful |
| Tests | xUnit + FluentAssertions |

### 3.1 LLM integration
* Canonical implementation of `ILlmClient` is **`OpenAiLlmClient`** (wrapper around OpenAI .NET SDK).  
* Env vars: `OPENAI_KEY`, `OPENAI_BASE` *(optional)*.  
* Retry governed by `RetryPolicy` table; override via `NOVELIST_MAX_RETRIES_<MODEL_ID>`.  
* Unit tests stay deterministic via `StubLlmClient`.

---

## 4. CLI verbs

| Verb | Purpose | Phase advanced |
|------|---------|----------------|
| `outline create` | Seed empty outline from project file. | Init → PremiseExpanded |
| `outline expand-premise` | Grow premise paragraph(s). | PremiseExpanded |
| `outline define-arc` | Generate three‑act (or multi‑act) story arc. | ArcDefined |
| `outline define-characters` | Produce character roster. | CharactersOutlined |
| `outline define-structure` | **NEW (v1.2)** – Generate chapter list with beats/themes. | StructureOutlined |

---

## 5. Repository Hygiene

* Use the supplied **`.gitignore`** (see root) and update only via full‑file replacement.  
* Build pipeline: `dotnet clean && dotnet build && dotnet test` must pass **warning‑as‑error**.  
* All JSON examples in `/samples` and `/tests/samples` must validate against the frozen schemas.

---

## 6. Licensing

* The project remains **private** for now; licence TBD (Apache 2.0 or MIT).  
* All author‑preset JSON files must include a `"license": "Public Domain (CC0)"` note in their header block.

---

## 7. Outstanding Items – **RESOLVED**

| ID | Decision |
|----|----------|
| Q1–Q5 | Folder layout, naming, overwrite mode, phase labels, and preset licensing finalised in v1.2. |
| Licence | Marked *pending* (see § 6). |

---

## 8. Milestones & Acceptance

Milestone ordering remains:  
1. Schema validation layer  
2. Stub outline generator  
3. Live LLM integration  
4. Scenes/Beats generator (*StructureOutlinerService*) ← current work  
5. Optional CLI harness enhancements  
See `/milestones_and_acceptance.md` for details.

---
