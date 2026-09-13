namespace UniversalPOS.Domain.Restaurant;

public class Floor
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<DiningTable> Tables { get; set; } = new List<DiningTable>();
}
