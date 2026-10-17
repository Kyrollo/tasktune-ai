namespace TaskTune.Application.DTOs.Users;

public record CreateUserDto(
    int CompanyId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    byte Role,
    int? DepartmentId,
    int? PositionId,
    string? Phone
);