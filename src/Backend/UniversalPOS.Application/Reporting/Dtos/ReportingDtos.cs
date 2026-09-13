namespace UniversalPOS.Application.Reporting.Dtos;

public class SalesSummaryDto
{
    public decimal GrossSales { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal ServiceChargeTotal { get; set; }
    public decimal NetSales { get; set; }
    public int TransactionCount { get; set; }
    public int VoidCount { get; set; }
    public decimal AverageSaleValue { get; set; }
}

public class SalesByProductDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class SalesByCategoryDto
{
    public long? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class SalesByCashierDto
{
    public long CashierUserId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal Revenue { get; set; }
}

public class SalesByPaymentMethodDto
{
    public string Method { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class StockValuationDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal CostPrice { get; set; }
    public decimal ValuationAtCost { get; set; }
}

public class DashboardSummaryDto
{
    public decimal TodayNetSales { get; set; }
    public int TodayTransactionCount { get; set; }
    public decimal Last7DaysNetSales { get; set; }
    public int LowStockProductCount { get; set; }
    public List<SalesByProductDto> TopProducts { get; set; } = new();
}
