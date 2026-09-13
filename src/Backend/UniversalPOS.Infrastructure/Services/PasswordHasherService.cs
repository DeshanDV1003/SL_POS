using Microsoft.AspNetCore.Identity;
using UniversalPOS.Application.Common.Interfaces;

namespace UniversalPOS.Infrastructure.Services;

/// <summary>Wraps ASP.NET Core Identity's PasswordHasher (PBKDF2), used with a marker type since we don't use full Identity.</summary>
public class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object HasherContext = new();

    public string Hash(string plainText) => _hasher.HashPassword(HasherContext, plainText);

    public bool Verify(string hash, string plainText)
    {
        var result = _hasher.VerifyHashedPassword(HasherContext, hash, plainText);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
