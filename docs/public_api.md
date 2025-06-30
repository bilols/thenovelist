```c#
public interface IOutlineBuilder
{
    Task<Outline> BuildAsync(
        Project project,
        IProgress<OutlineProgress> progress,
        CancellationToken ct = default);
}

public record OutlineProgress(string Phase, int Attempt, int Percent);
```

*`Phase`* values: Validation, PromptPrep, LlmPass, PostProcess.  
*`Attempt`* is 1‑based per phase (e.g., retries); *Percent* is coarse (0‑100).
---
