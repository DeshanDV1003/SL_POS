namespace UniversalPOS.Domain.Restaurant;

public class PreparationTicketLine
{
    public long Id { get; set; }
    public long PreparationTicketId { get; set; }
    public PreparationTicket PreparationTicket { get; set; } = null!;

    public long OrderLineId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}
