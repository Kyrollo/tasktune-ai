namespace TaskTune.Application.DTOs.Evaluations;

public record CreateEvaluationDto(
    int TaskId,
    int EvaluatedUserId,
    decimal OverallScore,
    decimal QualityScore,
    decimal TimelinessScore,
    decimal CollaborationScore,
    byte RiskLevel,
    string? ManualFeedback
);