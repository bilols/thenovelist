# Summary B – Draft-generation logic in Prototype 2 (`bilols/novelist2`)

_Last scanned: 2025-07-08._

---

## 1 · Key Python modules

| Path | Purpose |
|------|---------|
|`core/chapter_builder.py`|Primary class `ChapterBuilder` orchestrates piece-wise drafting, duplicate detection, and summary generation.|
|`worker/tasks/bulk_draft.py`|CLI/task entry-point that loops over chapters and calls `ChapterBuilder`.|
|`utils/cost_logger.py`|Extends the Prototype 1 cost helper by writing a **CSV row per LLM call** (`model, prompt_tokens, completion_tokens, cost`).|

---

## 2 · Dynamic piece algorithm

| Step | Logic |
|------|-------|
|1|`piece_size_words` configurable (default = 350 words).|
|2|Compute `piece_count = ceil(target_words / piece_size_words)` – varies per chapter.|
|3|Prompt instructs the model to write **exactly one piece**: tagged with `--- piece {idx}/{count} ---`.|
|4|After each piece:<br>• `ends_clean()` uses regex for `.?!—"`.<br>• `detect_duplicate_paragraphs(new_piece, accumulated_text)` – fuzzy string-match (ratio > 0.85) triggers retry.<br>• Beat / subplot coverage updated; remaining items listed in next prompt.|
|5|Retries up to **5** times per piece with escalating instructions (`expand`, `finish scene`, `remove repetition`).|
|6|When chapter reaches ±5 % target words, builder calls `summarise_text_120()` helper (another LLM prompt) and stores summary for next chapter.|

---

## 3 · Prompt skeleton (simplified)

```yaml
---
chapter: 1
piece: 3/9
goal_words: 350
remaining_beats:
  - confronts Mr Harding
remaining_subplots:
  - S2: Dreams intensify
---
Continue the story from the previous text.
Write only the next piece.
Do NOT repeat previous paragraphs.
End on a full stop.
````

*Previous text (trimmed to last 1500 tokens) is appended after the YAML.*

---

## 4 · Validation & retry rules

| Check                                  | Action                                        |
| -------------------------------------- | --------------------------------------------- |
| Duplicate paragraph (>85 % similarity) | Retry (`remove repetition`).                  |
| Word count < 250                       | Retry (`expand`).                             |
| Ends mid-sentence                      | Retry (`finish scene`).                       |
| Missing required beat/subplot          | Retry (`cover remaining beats`).              |
| After 5 failures                       | Piece is accepted but logged as `needs_edit`. |

---

## 5 · Cost logging

*`utils/cost_logger.py`*

```python
CostLogger.log(model, prompt_tokens, completion_tokens, USD)
# writes one CSV row per call + aggregates totals at end
```

---

## 6 · Improvements over Prototype 1

| Area                | V1                | V2                                       |
| ------------------- | ----------------- | ---------------------------------------- |
| Piece count         | Fixed 8           | Dynamic by `piece_size_words`.           |
| Chunk tag           | `[C##-P#]`        | YAML front-matter + `--- piece x/y ---`. |
| Duplicate detection | None              | Fuzzy ratio check.                       |
| Retry depth         | 3                 | 5 with escalating prompts.               |
| Cost tracking       | Totals only       | Per-call CSV.                            |
| Beat coverage       | Boolean checklist | Remaining-beat list passed each piece.   |

---

## 7 · Gaps that remain

* Still no mid-chapter **POV consistency** checks.
* Duplicate detection is paragraph-level; subtle sentence overlap slips
  through.
* Token window is static (1500 tokens); could adapt to model context.

---

## 8 · Take-aways for current C# DraftBuilder

1. **Adopt dynamic piece sizing** (`target_words / desired_piece_words`).
2. Use structured delimiter (`--- piece x/y ---`) or keep `[C##-P#]`; either
   works—markers just need to be parse-safe.
3. Port **duplicate-paragraph detection** (e.g., Levenshtein ratio > 0.85).
4. Escalating retry messages improve compliance; replicate.
5. Integrate **CSV cost logger** capturing model, tokens, USD per call—
   pluggable into future UI.
6. Maintain single-heading guard and running-summary flow from recent C#
   updates.

---

_End of Summary B_
