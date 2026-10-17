namespace TaskTune.Application.DTOs.Tasks;

public record CreateTaskDto(
    int ProjectId,
    string TaskName,
    string? Description,
    byte Priority,
    int AssignedTo,
    DateOnly StartDate,
    DateOnly DueDate,
    string? SubmitMethod,
    string? DefinitionOfDone,
    List<CreateTaskSubtaskDto>? Subtasks,
    object? AiRecommendation
);

public record CreateTaskSubtaskDto(
    string SubtaskName,
    string? Description,
    int? OrderIndex
);

