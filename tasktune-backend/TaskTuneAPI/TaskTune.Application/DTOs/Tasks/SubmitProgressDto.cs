namespace TaskTune.Application.DTOs.Tasks;

public record SubmitProgressDto(
    int CompletionPercentage,
    string? Notes,
    string? Blockers
);