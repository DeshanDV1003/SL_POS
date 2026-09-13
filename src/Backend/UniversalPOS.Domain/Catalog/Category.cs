using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Catalog;

public class Category : AuditableEntity
{
    public long CompanyId { get; set; }
    public long? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Default tax for products in this category that don't override it themselves.</summary>
    public long? DefaultTaxRateId { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
}
