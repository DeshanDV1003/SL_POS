namespace UniversalPOS.Application.Identity.Dtos;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Optional: the terminal this login is occurring on, embedded in the issued token.
    /// Terminal.Code is unique only per-Branch (e.g. every branch may have a "T1"), so
    /// BranchCode must be supplied alongside it to disambiguate.
    /// </summary>
    public string? TerminalCode { get; set; }
    public string? BranchCode { get; set; }
}
