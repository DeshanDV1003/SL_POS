using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Organization;

public class Branch : AuditableEntity
{
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Unique per Company. Used as the invoice/KOT numbering series key.</summary>
    public string Code { get; set; } = string.Empty;

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }

    public BusinessType BusinessTypeFlags { get; set; } = BusinessType.None;

    /// <summary>Service charge rate applied at billing, e.g. 0.10m for 10%. No SL statute mandates this; purely configurable.</summary>
    public decimal ServiceChargeRate { get; set; }

    /// <summary>If true, checkout requires the cashier to have an open CashierShift on the terminal — off by default so existing/simpler deployments aren't forced into shift discipline.</summary>
    public bool RequireOpenShiftForSale { get; set; }

    /// <summary>Hour (0-23, branch-local convention, stored/interpreted as UTC for now) at which a new "business day" starts for Z-report purposes — e.g. 3 means a 1am sale still counts as the previous day's trading.</summary>
    public int BusinessDayCutoffHour { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Terminal> Terminals { get; set; } = new List<Terminal>();
}
