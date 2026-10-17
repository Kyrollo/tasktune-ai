using System;

namespace TaskTuneAPI.Models.DTOs
{
    public class UserResponseDto
    {
        public int UserId { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public int? PositionId { get; set; }
        public string PositionTitle { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public byte Role { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateUserDto
    {
        public int CompanyId { get; set; }
        public int? DepartmentId { get; set; }
        public int? PositionId { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public byte Role { get; set; }
    }

    public class UpdateUserDto
    {
        public int? DepartmentId { get; set; }
        public int? PositionId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public byte? Role { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UserSummaryDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public byte Role { get; set; }
        public string RoleName { get; set; }
    }
}