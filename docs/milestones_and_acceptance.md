## Milestones & Acceptance

The following are the milestones and acceptance criteria for each:

| # | Deliverable | Acceptance Test |
|---|-------------|-----------------|
| 0 | Compiling skeleton | `dotnet test` passes 0 tests; `dotnet build` succeeds |
| 1 | Validation layer | Given sample `project.json`, `BuildAsync` completes Phase=Validation with 100% |
| 2 | Stub outline | Returns deterministic outline and passes schema validation |
| 3 | Real LLM | Uses live OpenAI, retries up to N, progress events fire; outline passes schema |
| 4 | (Optional CLI) | Running `dotnet run` on harness creates outline file |

Each milestone is delivered as **full files only** per Universal Rule #2.
---