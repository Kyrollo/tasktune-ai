namespace TaskTune.Application.DTOs.Users;

public record UpdateUserDto(
    string FirstName,
    string LastName,
    string? Phone,
    int? DepartmentId,
    int? PositionId
);

