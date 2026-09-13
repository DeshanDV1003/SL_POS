namespace UniversalPOS.Domain.Restaurant;

public enum TableStatus
{
    Available = 0,
    Occupied = 1,
    Reserved = 2,
    Cleaning = 3,
    Billing = 4,
}

/// <summary>Named DiningTable (not Table) to avoid the generic SQL-flavored name colliding with the DB's own vocabulary.</summary>
public class DiningTable
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long FloorId { get; set; }
    public Floor Floor { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public TableStatus Status { get; set; } = TableStatus.Available;
}
