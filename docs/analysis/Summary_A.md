# Summary A – Draft-generation logic in Prototype 1 (`bilols/novelist`)

_Last scanned: 2025-07-08._

---

## 1 · Key Python modules

| Path | Purpose |
|------|---------|
|`draft_driver/draft_driver.py`|Top-level orchestrator that generates each chapter in fixed-size pieces, validates length & scene endings, and updates a running summary.|
|`draft_driver/prompt_templates.py`|Provides the prompt skeletons (`CHAPTER_TEMPLATE`, `PIECE_TEMPLATE`).|
|`novelist_cli/llm/openai_wrapper.py`|Thin OpenAI wrapper that adds **token / cost tracking** (`CostTracker` class).|
|`utils/text_utils.py`|Helpers such as `ends_clean(text)` (checks last char), `PIECE_RE` regex (`\[C\d{2}-P\d]`).|

---

## 2 · Chunk (“piece”) algorithm

| Step | Logic |
|------|-------|
|1|Get `--pieces` argument (default = 8) → `piece_count`.|
|2|Compute `target_words = min_words * 1.2` (min ≈ total/chapters).|
|3|**Prompt** instructs the LLM to “Write Chapter N in **{piece_count} pieces**. Each piece must start with `[C{{chapter:02}}-P{{piece}}]` tag.”|
|4|Loop over pieces 1 … `piece_count`:<br>• Send prompt including **all text generated so far** (context).<br>• After reply, check:<br>&nbsp;&nbsp;– Starts with expected tag.<br>&nbsp;&nbsp;– `ends_clean()`.<br>&nbsp;&nbsp;– Word count ≥ `min_piece` (~target/pieces).<br>• If not, retry with “Finish the scene / Expand the piece.”|
|5|When all pieces done, merge them, write `chapter_##.md`.|
|6|Generate **120-word summary** of the chapter via another LLM call; prepend to next chapter prompt as `Prev-chapter summary:`.|

---

## 3 · Prompt skeleton (simplified)

```text
CHAPTER N – "Title"
Goal ≈ {target_words} words (≥{min_words}).

Beats:
 - ☒ Beat 1
 - ☒ Beat 2
 - ☒ Beat 3

Sub-plots:
 - S1: ...
 - S2: ...

Write Chapter N in **{piece_count} pieces**.
Each piece must start with: [C{N:02}-P{index}] on its own line.
Only write one piece at a time when prompted.

{{optional previous piece text}}
```

*When prompting for piece i, driver appends:*  
`Write piece {i}/{piece_count} now.`  
*For retries:*  
`Your previous attempt ended mid-sentence. Finish the scene.`

---

## 4 · Validation & retry rules

| Check | Action |
|-------|--------|
|Piece tag missing|Retry once with reminder.|
|Word count < `min_piece`|Retry with “Expand”.|
|Ends mid-sentence (`ends_clean` == False)|Retry with “Finish the scene”.|
|Duplicates (not implemented)|N/A in prototype 1.|

Maximum retries per piece: **3**.

---

## 5 · Token / cost tracking

*Located in* `novelist_cli/llm/openai_wrapper.py`

```python
CostTracker.record(model, prompt_tokens, completion_tokens)
CostTracker.total_cost()  # returns USD float
```

Rates hard-coded per model (GPT-3.5, GPT-4).

---

## 6 · Strengths & weaknesses

| Strength | Detail |
|----------|--------|
|Deterministic chunk structure|Piece tags prevent text overlap and help reassembly.|
|Beat completion checklist|Prompt visually flips ☒ → ✔ (model tends to remove ☒).|
|Cost tracking utility|Can be ported to current codebase.|
|Simple end-clean validator|Avoids cliffhangers in the middle of a sentence.|

| Weakness | Detail |
|----------|--------|
|Fixed piece count (8)|Not adaptive to chapter length or token limits.|
|No duplicate-sentence detection|Model occasionally rephrases previous piece.|
|Retry logic rudimentary|If length/check fails 3×, piece is accepted anyway.|

---

## 7 · Take-aways for current C# DraftBuilder

1. **Piece tag pattern** (`[C##-P#]`) is robust for recombination—worth porting.  
2. Generate dynamic `piece_count = ceil(target_words / desired_piece_words)`  
   (prototype uses constant 8).  
3. Keep `ends_clean()` style validation.  
4. Integrate a **token-cost tracker** like `CostTracker` for future UI display.  
5. Add duplicate-text detection (missing in prototype 1).

---

_End of Summary A_
