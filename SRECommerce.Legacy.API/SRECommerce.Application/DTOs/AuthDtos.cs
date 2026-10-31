namespace SRECommerce.Application.DTOs;

public record RegisterRequest(string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, string FullName, string Email, string Role, DateTime ExpiresAt);
public record AdminUserDto(int Id, string FullName, string Email, string Role, DateTime CreatedAt);
public record CreateStaffUserRequest(string FullName, string Email, string Password, string Role);

