namespace UniversalPOS.Application.Identity.Dtos;

public class LoginResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public UserProfileDto User { get; set; } = null!;
}

public class UserProfileDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public long CompanyId { get; set; }
    public IReadOnlyList<long> BranchIds { get; set; } = Array.Empty<long>();
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}
