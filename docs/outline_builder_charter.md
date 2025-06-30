# Outline Builder – Subsystem Charter

**Goal:** Transform `project.json` → `outline.json` through multi‑pass OpenAI prompting, exposing progress and supporting retries, while validating against `outline.schema.v2.json`.

---

## 1. Schema Updates (v2)

### outline.schema.v2.json – New / changed top‑level properties
| Property | Type | Required | Notes |
|----------|------|----------|-------|
| `chapters` | array<Chapter> | yes | unchanged |
| `prologue` | Section or null | no | new, optional |
| `epilogue` | Section or null | no | new, optional |
| `stylePreset` | string | yes | e.g. `"StephenKing"`; must match a file in `/author_presets` |

### Section object
```json
{
  "title": "string",
  "summary": "string",
  "targetWordCount": "integer"
}
```

**targetWordCount** defaults to half of **averageChapterWordCount** if omitted.
Validation rules (beyond JSON Schema):   

* Total chapters.Length must match projectJson.targetChapterCount.
* Sum(chapter.wordCount) must equal projectJson.targetWordCount.
* Prologue/epilogue word‑counts are excluded from the above sum.
---
