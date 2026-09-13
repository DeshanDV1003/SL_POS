namespace UniversalPOS.Domain.Identity;

public class UserRole
{
    public long UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
