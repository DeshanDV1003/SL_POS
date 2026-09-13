namespace UniversalPOS.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string plainText);

    /// <returns>True if plainText matches the hash.</returns>
    bool Verify(string hash, string plainText);
}
