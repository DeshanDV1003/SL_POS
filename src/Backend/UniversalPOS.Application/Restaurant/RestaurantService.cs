using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Restaurant.Dtos;
using UniversalPOS.Application.Sales;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Auditing;
using UniversalPOS.Domain.Restaurant;

namespace UniversalPOS.Application.Restaurant;

public class RestaurantService : IRestaurantService
{
    private readonly IApplicationDbContext _db;
    private readonly ISalesService _salesService;
    private readonly IValidator<AddOrderLineRequest> _lineValidator;
    private readonly IValidator<CancelTicketRequest> _cancelTicketValidator;
    private readonly IValidator<CreateStandaloneOrderRequest> _standaloneOrderValidator;

    public RestaurantService(
        IApplicationDbContext db,
        ISalesService salesService,
        IValidator<AddOrderLineRequest> lineValidator,
        IValidator<CancelTicketRequest> cancelTicketValidator,
        IValidator<CreateStandaloneOrderRequest> standaloneOrderValidator)
    {
        _db = db;
        _salesService = salesService;
        _lineValidator = lineValidator;
        _cancelTicketValidator = cancelTicketValidator;
        _standaloneOrderValidator = standaloneOrderValidator;
    }

    public async Task<IReadOnlyList<FloorDto>> GetFloorsAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        var floors = await _db.Floors
            .Include(f => f.Tables)
            .Where(f => f.CompanyId == companyId && f.BranchId == branchId)
            .OrderBy(f => f.SortOrder)
            .ToListAsync(cancellationToken);

        var openOrdersByTable = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.BranchId == branchId && o.Status == OrderStatus.Open && o.TableSessionId != null)
            .Join(_db.TableSessions, o => o.TableSessionId, s => s.Id, (o, s) => new { o.Id, s.TableId })
            .ToDictionaryAsync(x => x.TableId, x => x.Id, cancellationToken);

        return floors.Select(f => new FloorDto
        {
            Id = f.Id,
            Name = f.Name,
            Tables = f.Tables.Select(t => new TableDto
            {
                Id = t.Id,
                FloorId = t.FloorId,
                Name = t.Name,
                Capacity = t.Capacity,
                Status = t.Status.ToString(),
                OpenOrderId = openOrdersByTable.TryGetValue(t.Id, out var openOrderId) ? openOrderId : null,
            }).ToList(),
        }).ToList();
    }

    public async Task<OrderDto> OpenTableAsync(long companyId, long branchId, long tableId, long waiterUserId, OpenTableRequest request, CancellationToken cancellationToken = default)
    {
        var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == tableId && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(DiningTable), tableId);

        if (table.Status != TableStatus.Available)
        {
            throw new ConflictException($"Table '{table.Name}' is not available ({table.Status}).");
        }

        var session = new TableSession
        {
            CompanyId = companyId,
            BranchId = branchId,
            TableId = tableId,
            WaiterUserId = waiterUserId,
            Status = TableSessionStatus.Open,
            OpenedAtUtc = DateTime.UtcNow,
        };
        _db.TableSessions.Add(session);

        table.Status = TableStatus.Occupied;

        var order = new Order
        {
            CompanyId = companyId,
            BranchId = branchId,
            OrderType = request.OrderType,
            Status = OrderStatus.Open,
            CreatedByUserId = waiterUserId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        order.TableSessionId = session.Id;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetOrderAsync(companyId, order.Id, cancellationToken);
    }

    public async Task<OrderDto> CreateStandaloneOrderAsync(long companyId, long branchId, long userId, CreateStandaloneOrderRequest request, CancellationToken cancellationToken = default)
    {
        await _standaloneOrderValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (request.OrderType == OrderType.DineIn)
        {
            throw new ConflictException("A dine-in order must be opened against a table — use the open-table endpoint instead.");
        }

        var order = new Order
        {
            CompanyId = companyId,
            BranchId = branchId,
            OrderType = request.OrderType,
            Status = OrderStatus.Open,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            ContactPhone = request.ContactPhone,
            DeliveryAddress = request.DeliveryAddress,
            DeliveryFee = request.DeliveryFee,
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetOrderAsync(companyId, order.Id, cancellationToken);
    }

    public async Task<OrderDto> TransferTableAsync(long companyId, long orderId, long userId, TransferTableRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), orderId);

        if (order.Status != OrderStatus.Open || !order.TableSessionId.HasValue)
        {
            throw new ConflictException($"Order {orderId} is not an open, seated dine-in order and cannot be transferred.");
        }

        var session = await _db.TableSessions.FirstAsync(s => s.Id == order.TableSessionId.Value, cancellationToken);
        var oldTable = await _db.DiningTables.FirstAsync(t => t.Id == session.TableId, cancellationToken);

        var newTable = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == request.NewTableId && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(DiningTable), request.NewTableId);

        if (newTable.Id == oldTable.Id)
        {
            throw new ConflictException("Cannot transfer a table to itself.");
        }
        if (newTable.Status != TableStatus.Available)
        {
            throw new ConflictException($"Table '{newTable.Name}' is not available ({newTable.Status}).");
        }

        oldTable.Status = TableStatus.Available;
        newTable.Status = TableStatus.Occupied;
        session.TableId = newTable.Id;

        await _db.SaveChangesAsync(cancellationToken);
        return await GetOrderAsync(companyId, orderId, cancellationToken);
    }

    public async Task<OrderDto> MergeOrdersAsync(long companyId, long sourceOrderId, long userId, MergeOrdersRequest request, CancellationToken cancellationToken = default)
    {
        if (sourceOrderId == request.TargetOrderId)
        {
            throw new ConflictException("Cannot merge an order into itself.");
        }

        var source = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == sourceOrderId && o.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), sourceOrderId);
        var target = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == request.TargetOrderId && o.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.TargetOrderId);

        if (source.Status != OrderStatus.Open || target.Status != OrderStatus.Open)
        {
            throw new ConflictException("Both orders must be open to merge them.");
        }

        // Re-parent the lines rather than copying them, so each line's KOT history
        // (and any PreparationTicketLine referencing it) stays intact.
        foreach (var line in source.Lines.ToList())
        {
            line.OrderId = target.Id;
        }

        source.Status = OrderStatus.Cancelled;

        if (source.TableSessionId.HasValue)
        {
            var session = await _db.TableSessions.FirstAsync(s => s.Id == source.TableSessionId.Value, cancellationToken);
            session.Status = TableSessionStatus.Closed;
            session.ClosedAtUtc = DateTime.UtcNow;

            var table = await _db.DiningTables.FirstAsync(t => t.Id == session.TableId, cancellationToken);
            table.Status = TableStatus.Available;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetOrderAsync(companyId, target.Id, cancellationToken);
    }

    public async Task<OrderDto> SplitOrderAsync(long companyId, long branchId, long orderId, long userId, SplitOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OrderLineIds.Count == 0)
        {
            throw new ConflictException("At least one line must be selected to split off.");
        }

        var source = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), orderId);

        if (source.Status != OrderStatus.Open)
        {
            throw new ConflictException($"Order {orderId} is not open and cannot be split.");
        }

        var linesToMove = source.Lines.Where(l => request.OrderLineIds.Contains(l.Id)).ToList();
        if (linesToMove.Count != request.OrderLineIds.Count)
        {
            throw new NotFoundException("OrderLine", string.Join(",", request.OrderLineIds));
        }
        if (linesToMove.Count == source.Lines.Count)
        {
            throw new ConflictException("Cannot split every line off an order — use table transfer instead if the whole order is moving.");
        }

        var newTable = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == request.NewTableId && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(DiningTable), request.NewTableId);
        if (newTable.Status != TableStatus.Available)
        {
            throw new ConflictException($"Table '{newTable.Name}' is not available ({newTable.Status}).");
        }

        var newSession = new TableSession
        {
            CompanyId = companyId,
            BranchId = branchId,
            TableId = newTable.Id,
            WaiterUserId = userId,
            Status = TableSessionStatus.Open,
            OpenedAtUtc = DateTime.UtcNow,
        };
        _db.TableSessions.Add(newSession);
        newTable.Status = TableStatus.Occupied;

        var newOrder = new Order
        {
            CompanyId = companyId,
            BranchId = branchId,
            OrderType = source.OrderType,
            Status = OrderStatus.Open,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Orders.Add(newOrder);
        await _db.SaveChangesAsync(cancellationToken);

        newOrder.TableSessionId = newSession.Id;
        foreach (var line in linesToMove)
        {
            line.OrderId = newOrder.Id;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetOrderAsync(companyId, newOrder.Id, cancellationToken);
    }

    public async Task<OrderDto> GetOrderAsync(long companyId, long orderId, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), orderId);

        long? tableId = null;
        if (order.TableSessionId.HasValue)
        {
            tableId = await _db.TableSessions.Where(s => s.Id == order.TableSessionId.Value).Select(s => (long?)s.TableId).FirstOrDefaultAsync(cancellationToken);
        }

        var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        return ToOrderDto(order, tableId, products);
    }

    public async Task<OrderDto> AddOrderLinesAsync(long companyId, long orderId, List<AddOrderLineRequest> lines, CancellationToken cancellationToken = default)
    {
        foreach (var line in lines)
        {
            await _lineValidator.ValidateAndThrowAsync(line, cancellationToken);
        }

        var order = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), orderId);

        if (order.Status != OrderStatus.Open)
        {
            throw new ConflictException($"Order {orderId} is not open ({order.Status}) and cannot accept new items.");
        }

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var validCount = await _db.Products.CountAsync(p => p.CompanyId == companyId && productIds.Contains(p.Id), cancellationToken);
        if (validCount != productIds.Count)
        {
            throw new NotFoundException("Product", string.Join(",", productIds));
        }

        foreach (var line in lines)
        {
            order.Lines.Add(new OrderLine
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                Notes = line.Notes,
                KotStatus = KotLineStatus.Pending,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetOrderAsync(companyId, orderId, cancellationToken);
    }

    public async Task<IReadOnlyList<PreparationTicketDto>> SendToKitchenAsync(long companyId, long branchId, long orderId, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), orderId);

        var pendingLines = order.Lines.Where(l => l.KotStatus == KotLineStatus.Pending).ToList();
        if (pendingLines.Count == 0)
        {
            return Array.Empty<PreparationTicketDto>();
        }

        var productIds = pendingLines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var groups = pendingLines
            .Where(l => products[l.ProductId].DefaultKitchenStationId.HasValue)
            .GroupBy(l => products[l.ProductId].DefaultKitchenStationId!.Value);

        var createdTickets = new List<PreparationTicket>();
        var sequence = await _db.PreparationTickets.CountAsync(t => t.BranchId == branchId, cancellationToken);

        foreach (var group in groups)
        {
            sequence++;
            var ticket = new PreparationTicket
            {
                CompanyId = companyId,
                BranchId = branchId,
                OrderId = orderId,
                KitchenStationId = group.Key,
                TicketNumber = $"T-{branchId}-{sequence:D6}",
                Status = TicketStatus.Sent,
                CreatedAtUtc = DateTime.UtcNow,
            };

            foreach (var line in group)
            {
                ticket.Lines.Add(new PreparationTicketLine
                {
                    OrderLineId = line.Id,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    Notes = line.Notes,
                });
                line.KotStatus = KotLineStatus.Sent;
            }

            _db.PreparationTickets.Add(ticket);
            createdTickets.Add(ticket);
        }

        // Items with no routed station (DefaultKitchenStationId is null) are marked
        // Sent without a ticket — they need no kitchen preparation step (e.g. a
        // pre-packaged item a waiter serves directly).
        foreach (var line in pendingLines.Where(l => !products[l.ProductId].DefaultKitchenStationId.HasValue))
        {
            line.KotStatus = KotLineStatus.Sent;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return createdTickets.Select(t => ToTicketDto(t, null)).ToList();
    }

    public async Task<IReadOnlyList<KitchenStation>> GetKitchenStationsAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
        => await _db.KitchenStations.Where(s => s.CompanyId == companyId && s.BranchId == branchId && s.IsActive).OrderBy(s => s.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PreparationTicketDto>> GetKdsTicketsAsync(long companyId, long branchId, long stationId, CancellationToken cancellationToken = default)
    {
        var tickets = await _db.PreparationTickets
            .Include(t => t.Lines)
            .Where(t => t.CompanyId == companyId && t.BranchId == branchId && t.KitchenStationId == stationId
                && t.Status != TicketStatus.Served && t.Status != TicketStatus.Cancelled)
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var orderIds = tickets.Select(t => t.OrderId).Distinct().ToList();
        var tableByOrder = await _db.Orders
            .Where(o => orderIds.Contains(o.Id) && o.TableSessionId != null)
            .Join(_db.TableSessions, o => o.TableSessionId, s => s.Id, (o, s) => new { o.Id, s.TableId })
            .ToDictionaryAsync(x => x.Id, x => x.TableId, cancellationToken);

        var productIds = tickets.SelectMany(t => t.Lines.Select(l => l.ProductId)).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        return tickets.Select(t => ToTicketDto(t, tableByOrder.TryGetValue(t.OrderId, out var tId) ? tId : null, products)).ToList();
    }

    public async Task<PreparationTicketDto> UpdateTicketStatusAsync(long companyId, long ticketId, UpdateTicketStatusRequest request, CancellationToken cancellationToken = default)
    {
        var ticket = await _db.PreparationTickets.Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(PreparationTicket), ticketId);

        ticket.Status = request.Status;
        if (request.Status == TicketStatus.Ready)
        {
            ticket.ReadyAtUtc = DateTime.UtcNow;
        }

        if (request.Status is TicketStatus.Served or TicketStatus.Cancelled)
        {
            var orderLineIds = ticket.Lines.Select(l => l.OrderLineId).ToList();
            var orderLines = await _db.OrderLines.Where(l => orderLineIds.Contains(l.Id)).ToListAsync(cancellationToken);
            var mappedStatus = request.Status == TicketStatus.Served ? Domain.Restaurant.KotLineStatus.Served : Domain.Restaurant.KotLineStatus.Cancelled;
            foreach (var line in orderLines)
            {
                line.KotStatus = mappedStatus;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToTicketDto(ticket, null);
    }

    public async Task<PreparationTicketDto> CancelTicketAsync(long companyId, long branchId, long? terminalId, long userId, long ticketId, CancelTicketRequest request, CancellationToken cancellationToken = default)
    {
        await _cancelTicketValidator.ValidateAndThrowAsync(request, cancellationToken);

        var ticket = await _db.PreparationTickets.Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(PreparationTicket), ticketId);

        if (ticket.Status is TicketStatus.Served or TicketStatus.Cancelled)
        {
            throw new ConflictException($"Ticket {ticket.TicketNumber} is already {ticket.Status} and cannot be cancelled.");
        }

        var previousStatus = ticket.Status;
        ticket.Status = TicketStatus.Cancelled;

        var orderLineIds = ticket.Lines.Select(l => l.OrderLineId).ToList();
        var orderLines = await _db.OrderLines.Where(l => orderLineIds.Contains(l.Id)).ToListAsync(cancellationToken);
        foreach (var line in orderLines)
        {
            line.KotStatus = KotLineStatus.Cancelled;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            CompanyId = companyId,
            BranchId = branchId,
            TerminalId = terminalId,
            UserId = userId,
            ActionCode = "Kot.Cancel",
            EntityType = nameof(PreparationTicket),
            EntityId = ticket.Id.ToString(),
            OldValueJson = System.Text.Json.JsonSerializer.Serialize(new { Status = previousStatus.ToString() }),
            NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { Status = nameof(TicketStatus.Cancelled), request.Reason }),
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return ToTicketDto(ticket, null);
    }

    public async Task<SaleReceiptDto> BillOrderAsync(long companyId, long branchId, long cashierUserId, long orderId, BillOrderRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), orderId);

        if (order.Status != OrderStatus.Open)
        {
            throw new ConflictException($"Order {orderId} is not open ({order.Status}) and cannot be billed.");
        }

        var activeLines = order.Lines.Where(l => l.KotStatus != Domain.Restaurant.KotLineStatus.Cancelled).ToList();
        if (activeLines.Count == 0)
        {
            throw new ConflictException("An order with no active items cannot be billed.");
        }

        var saleRequest = new CreateSaleRequest
        {
            TerminalId = request.TerminalId,
            Lines = activeLines.Select(l => new CreateSaleLineRequest { ProductId = l.ProductId, Quantity = l.Quantity, DiscountPercentage = 0 }).ToList(),
            Payments = request.Payments,
        };

        var receipt = await _salesService.CheckoutAsync(companyId, branchId, cashierUserId, saleRequest, cancellationToken);

        order.Status = OrderStatus.Billed;
        order.SaleHeaderId = receipt.Id;

        if (order.TableSessionId.HasValue)
        {
            var session = await _db.TableSessions.FirstAsync(s => s.Id == order.TableSessionId.Value, cancellationToken);
            session.Status = TableSessionStatus.Closed;
            session.ClosedAtUtc = DateTime.UtcNow;

            var table = await _db.DiningTables.FirstAsync(t => t.Id == session.TableId, cancellationToken);
            table.Status = TableStatus.Available;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return receipt;
    }

    private static OrderDto ToOrderDto(Order order, long? tableId, Dictionary<long, Domain.Catalog.Product> products) => new()
    {
        Id = order.Id,
        TableId = tableId,
        OrderType = order.OrderType.ToString(),
        Status = order.Status.ToString(),
        CreatedAtUtc = order.CreatedAtUtc,
        ContactPhone = order.ContactPhone,
        DeliveryAddress = order.DeliveryAddress,
        DeliveryFee = order.DeliveryFee,
        Lines = order.Lines.Select(l => new OrderLineDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ProductName = products.TryGetValue(l.ProductId, out var p) ? p.Name : "(unknown product)",
            Quantity = l.Quantity,
            Notes = l.Notes,
            KotStatus = l.KotStatus.ToString(),
        }).ToList(),
    };

    private static PreparationTicketDto ToTicketDto(PreparationTicket ticket, long? tableId, Dictionary<long, Domain.Catalog.Product>? products = null) => new()
    {
        Id = ticket.Id,
        TicketNumber = ticket.TicketNumber,
        OrderId = ticket.OrderId,
        TableId = tableId,
        Status = ticket.Status.ToString(),
        CreatedAtUtc = ticket.CreatedAtUtc,
        Lines = ticket.Lines.Select(l => new PreparationTicketLineDto
        {
            ProductId = l.ProductId,
            ProductName = products != null && products.TryGetValue(l.ProductId, out var p) ? p.Name : string.Empty,
            Quantity = l.Quantity,
            Notes = l.Notes,
        }).ToList(),
    };
}
