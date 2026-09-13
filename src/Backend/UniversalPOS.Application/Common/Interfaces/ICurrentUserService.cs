namespace UniversalPOS.Application.Common.Interfaces;

/// <summary>Resolves identity from the current HTTP request's validated JWT claims.</summary>
public interface ICurrentUserService
{
    long? UserId { get; }
    long CompanyId { get; }
    long? TerminalId { get; }
    bool HasPermission(string code);
}
