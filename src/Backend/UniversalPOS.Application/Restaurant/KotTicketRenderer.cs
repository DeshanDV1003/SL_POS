using System.Text;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Restaurant;

namespace UniversalPOS.Application.Restaurant;

public class KotTicketRenderer : IKotTicketRenderer
{
    private const int Width = 32; // kitchen/bar printers are typically narrower (58mm) than an 80mm receipt printer.

    private readonly IApplicationDbContext _db;

    public KotTicketRenderer(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> RenderAsync(long companyId, long ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await _db.PreparationTickets.Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(PreparationTicket), ticketId);

        var station = await _db.KitchenStations.FirstAsync(s => s.Id == ticket.KitchenStationId, cancellationToken);
        var order = await _db.Orders.FirstAsync(o => o.Id == ticket.OrderId, cancellationToken);

        var productIds = ticket.Lines.Select(l => l.ProductId).Distinct().ToList();
        var productNames = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var sb = new StringBuilder();

        var ticketKind = station.Category == StationCategory.Bar ? "BAR ORDER TICKET" : "KITCHEN ORDER TICKET";
        sb.AppendLine(Center(ticketKind, Width));
        sb.AppendLine(Center(station.Name, Width));
        sb.AppendLine(new string('-', Width));

        sb.AppendLine($"Ticket: {ticket.TicketNumber}");

        if (order.TableSessionId.HasValue)
        {
            var tableSession = await _db.TableSessions.FirstAsync(s => s.Id == order.TableSessionId.Value, cancellationToken);
            var table = await _db.DiningTables.FirstAsync(t => t.Id == tableSession.TableId, cancellationToken);
            sb.AppendLine($"Table: {table.Name}");
        }
        else
        {
            sb.AppendLine($"Order Type: {order.OrderType}");
            if (!string.IsNullOrWhiteSpace(order.ContactPhone)) sb.AppendLine($"Phone: {order.ContactPhone}");
        }

        sb.AppendLine($"Time: {ticket.CreatedAtUtc:HH:mm}");
        sb.AppendLine(new string('-', Width));

        foreach (var line in ticket.Lines)
        {
            var name = productNames.GetValueOrDefault(line.ProductId, "(item)");
            sb.AppendLine($"{line.Quantity:0.###}x {Truncate(name, Width - 5)}");
            if (!string.IsNullOrWhiteSpace(line.Notes))
            {
                sb.AppendLine($"  * {Truncate(line.Notes, Width - 4)}");
            }
        }

        sb.AppendLine(new string('-', Width));
        if (ticket.Status == TicketStatus.Cancelled)
        {
            sb.AppendLine(Center("*** CANCELLED ***", Width));
        }

        return sb.ToString();
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text[..width];
        var padding = (width - text.Length) / 2;
        return new string(' ', padding) + text;
    }

    private static string Truncate(string text, int width) => text.Length > width ? text[..width] : text;
}
