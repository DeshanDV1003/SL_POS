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

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
