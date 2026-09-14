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

public class SalesTrendPointDto
{
    public DateOnly Date { get; set; }
    public decimal NetSales { get; set; }
    public int TransactionCount { get; set; }
}

public class BranchComparisonDto
{
    public long BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public decimal NetSales { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageSaleValue { get; set; }
}

public class DiscountReportDto
{
    public decimal LineDiscountTotal { get; set; }
    public decimal CouponDiscountTotal { get; set; }
    public int SalesWithDiscountCount { get; set; }
    public decimal AverageDiscountPerDiscountedSale { get; set; }
    public List<SalesByProductDto> TopDiscountedProducts { get; set; } = new();
}

public class TaxByRateDto
{
    public decimal TaxRatePercentage { get; set; }
    public decimal TaxableRevenue { get; set; }
    public decimal TaxCollected { get; set; }
}

public class TaxReportDto
{
    public decimal TotalTaxCollected { get; set; }
    public List<TaxByRateDto> ByRate { get; set; } = new();
}

public class KitchenStationPerformanceDto
{
    public long KitchenStationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public int TicketCount { get; set; }
    public int CancelledTicketCount { get; set; }
    public decimal? AveragePrepTimeMinutes { get; set; }
}

public class WaiterPerformanceDto
{
    public long WaiterUserId { get; set; }
    public string WaiterName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int CancelledTicketCount { get; set; }
    public decimal BilledRevenue { get; set; }
}

public class SlowMovingStockDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public DateTime? LastSoldAtUtc { get; set; }
    public int? DaysSinceLastSale { get; set; }
}
