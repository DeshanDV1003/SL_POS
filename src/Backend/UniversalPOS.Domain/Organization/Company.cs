using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Organization;

public class Company : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? BusinessRegistrationNo { get; set; }

    /// <summary>9-digit IRD Taxpayer Identification Number, required for full VAT tax invoices.</summary>
    public string? TaxRegistrationNo { get; set; }
    public bool IsVatRegistered { get; set; }
    public DateTime? VatRegisteredFromDate { get; set; }

    public string DefaultCurrencyCode { get; set; } = "LKR";
    public string TimeZoneId { get; set; } = "Sri Lanka Standard Time";
    public bool IsActive { get; set; } = true;

    /// <summary>Points earned per unit of GrandTotal spent — default 0.01 (1 point per LKR 100), previously a hardcoded constant.</summary>
    public decimal LoyaltyPointsPerCurrencyUnit { get; set; } = 0.01m;

    /// <summary>Currency value of one point when paying with points at checkout — default LKR 1.00 per point.</summary>
    public decimal LoyaltyPointRedemptionValue { get; set; } = 1.00m;

    /// <summary>Months after which earned points expire; null means points never expire.</summary>
    public int? LoyaltyPointsExpiryMonths { get; set; }

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
