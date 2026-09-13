namespace UniversalPOS.Application.Crm.Dtos;

public class CustomerGroupDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DefaultDiscountPercentage { get; set; }
}

public class CreateCustomerGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal DefaultDiscountPercentage { get; set; }
}

public class CustomerDto
{
    public long Id { get; set; }
    public long? CustomerGroupId { get; set; }
    public long? MembershipTierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal OutstandingBalance { get; set; }
    public int LoyaltyPointsBalance { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCustomerRequest
{
    public long? CustomerGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxRegistrationNo { get; set; }
    public decimal CreditLimit { get; set; }
}
