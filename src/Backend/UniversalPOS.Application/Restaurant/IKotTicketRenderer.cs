namespace UniversalPOS.Application.Restaurant;

/// <summary>
/// Renders a PreparationTicket (a KOT or BOT — same record type, see
/// docs/database-design.md §5) as plain text formatted for a kitchen/bar printer —
/// the actual payload a hardware print adapter would send verbatim, per
/// docs/architecture.md §11 (business logic never talks to hardware directly, it
/// only produces the data). Fills the gap left open at the end of Phase 7's
/// gap-fill ("printed KOT/BOT tickets... hasn't yet").
/// </summary>
public interface IKotTicketRenderer
{
    Task<string> RenderAsync(long companyId, long ticketId, CancellationToken cancellationToken = default);
}
