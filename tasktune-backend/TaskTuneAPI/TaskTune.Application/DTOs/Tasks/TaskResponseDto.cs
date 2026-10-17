namespace TaskTune.Application.DTOs.Tasks;

public record TaskResponseDto(
    int TaskId,
    int ProjectId,
    string ProjectName,
    string TaskName,
    string? Description,
    byte Priority,
    byte Status,
    int AssignedTo,
    string AssignedToName,
    int CreatedBy,
    DateOnly StartDate,
    DateOnly DueDate,
    int CompletionPercentage,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? SubmitMethod,
    string? DefinitionOfDone,
    object? AiRecommendation
);
