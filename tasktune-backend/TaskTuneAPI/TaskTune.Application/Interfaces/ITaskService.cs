using TaskTune.Application.DTOs;
using TaskTune.Application.DTOs.Tasks;

namespace TaskTune.Application.Interfaces;

public interface ITaskService
{
    Task<PagedResultDto<TaskResponseDto>> GetTasksAsync(int userId, int page, int pageSize);
    Task<TaskResponseDto?> GetByIdAsync(int taskId, int requestedBy);
    Task<TaskResponseDto> CreateAsync(CreateTaskDto dto, int createdBy);
    Task<TaskResponseDto?> UpdateAsync(int taskId, CreateTaskDto dto, int updatedBy);
    Task<bool> DeleteAsync(int taskId, int deletedBy);
    Task<bool> SubmitProgressAsync(int taskId, SubmitProgressDto dto, int userId);
    Task<object> GetProgressAsync(int taskId, int requestedBy);
}
