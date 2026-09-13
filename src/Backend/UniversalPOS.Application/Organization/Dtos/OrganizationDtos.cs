namespace UniversalPOS.Application.Organization.Dtos;

public class CompanyDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string DefaultCurrencyCode { get; set; } = string.Empty;
    public bool IsVatRegistered { get; set; }
    public bool IsActive { get; set; }
}

public class BranchDto
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? City { get; set; }
    public string BusinessTypeFlags { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class TerminalDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
}
