namespace UniversalPOS.Domain.Inventory;

public enum StockMovementType
{
    Opening = 0,
    Purchase = 1,
    Sale = 2,
    SaleReturn = 3,
    PurchaseReturn = 4,
    TransferOut = 5,
    TransferIn = 6,
    AdjustmentIncrease = 7,
    AdjustmentDecrease = 8,
    Wastage = 9,
    StockCount = 10,
}
