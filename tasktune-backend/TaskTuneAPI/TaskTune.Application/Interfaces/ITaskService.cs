using TaskTune.Application.DTOs;
using TaskTune.Application.DTOs.Tasks;

namespace TaskTune.Application.Interfaces;

public interface ITaskService
{
    Task<PagedResultDto<TaskResponseDto>> GetTasksAsync(int userId, int page, int pageSize);
    Task<TaskResponseDto?> GetByIdAsync(int taskId);
    Task<TaskResponseDto> CreateAsync(CreateTaskDto dto, int createdBy);
    Task<TaskResponseDto?> UpdateAsync(int taskId, CreateTaskDto dto);
    Task<bool> DeleteAsync(int taskId);
    Task<bool> SubmitProgressAsync(int taskId, SubmitProgressDto dto, int userId);
    Task<object> GetProgressAsync(int taskId);
}

