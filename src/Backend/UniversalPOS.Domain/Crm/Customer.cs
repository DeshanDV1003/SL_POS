using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Crm;

public class Customer : AuditableEntity
{
    public long CompanyId { get; set; }
    public long? CustomerGroupId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    /// <summary>Captured so a VAT-registered customer can be issued a full tax invoice with purchaser TIN (mandatory format effective 1 Jul 2026 — see docs/research-sri-lanka-pos.md).</summary>
    public string? TaxRegistrationNo { get; set; }

    public decimal CreditLimit { get; set; }
    public decimal OutstandingBalance { get; set; }

    /// <summary>Current balance. All changes must go through an immutable LoyaltyTransaction ledger (added in Phase 9) — never mutated directly outside that path.</summary>
    public int LoyaltyPointsBalance { get; set; }

    public bool IsActive { get; set; } = true;
}
