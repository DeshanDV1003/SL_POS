using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Inventory;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Domain.Inventory;
using UniversalPOS.Domain.Purchasing;

namespace UniversalPOS.Application.Purchasing;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IApplicationDbContext _db;
    private readonly IStockService _stockService;
    private readonly IValidator<CreatePurchaseOrderRequest> _createValidator;
    private readonly IValidator<ReceiveGoodsRequest> _receiveValidator;
    private readonly IValidator<CreatePurchaseInvoiceRequest> _invoiceValidator;
    private readonly IValidator<RecordSupplierPaymentRequest> _paymentValidator;

    public PurchaseOrderService(
        IApplicationDbContext db,
        IStockService stockService,
        IValidator<CreatePurchaseOrderRequest> createValidator,
        IValidator<ReceiveGoodsRequest> receiveValidator,
        IValidator<CreatePurchaseInvoiceRequest> invoiceValidator,
        IValidator<RecordSupplierPaymentRequest> paymentValidator)
    {
        _db = db;
        _stockService = stockService;
        _createValidator = createValidator;
        _receiveValidator = receiveValidator;
        _invoiceValidator = invoiceValidator;
        _paymentValidator = paymentValidator;
    }

    public async Task<PurchaseOrderDto> CreateAsync(long companyId, long branchId, long createdByUserId, CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && s.CompanyId == companyId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Purchasing.Supplier), request.SupplierId);
        }

        var sequence = await _db.PurchaseOrders.CountAsync(po => po.BranchId == branchId, cancellationToken) + 1;
        var orderNumber = $"PO-{branchId}-{sequence:D6}";

        var order = new PurchaseOrder
        {
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = request.SupplierId,
            OrderNumber = orderNumber,
            Status = PurchaseOrderStatus.Submitted,
            CreatedByUserId = createdByUserId,
            OrderDate = DateTime.UtcNow,
            ExpectedDate = request.ExpectedDate,
            Notes = request.Notes,
        };

        foreach (var line in request.Lines)
        {
            order.Lines.Add(new PurchaseOrderLine { ProductId = line.ProductId, QuantityOrdered = line.Quantity, UnitCost = line.UnitCost });
        }

        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(order);
    }

    public async Task<PurchaseOrderDto> ApproveAsync(long companyId, long purchaseOrderId, long approvedByUserId, CancellationToken cancellationToken = default)
    {
        var order = await _db.PurchaseOrders
            .Include(po => po.Lines)
            .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.CompanyId == companyId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);
        }

        if (order.Status != PurchaseOrderStatus.Submitted)
        {
            throw new ConflictException($"Purchase order {order.OrderNumber} is not awaiting approval.");
        }

        order.Status = PurchaseOrderStatus.Approved;
        order.ApprovedByUserId = approvedByUserId;
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(order);
    }

    public async Task<IReadOnlyList<PurchaseOrderDto>> GetAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        var orders = await _db.PurchaseOrders
            .Include(po => po.Lines)
            .Where(po => po.CompanyId == companyId && po.BranchId == branchId)
            .OrderByDescending(po => po.OrderDate)
            .ToListAsync(cancellationToken);

        return orders.Select(ToDto).ToList();
    }

    public async Task<GoodsReceivedNoteDto> ReceiveGoodsAsync(long companyId, long branchId, long receivedByUserId, ReceiveGoodsRequest request, CancellationToken cancellationToken = default)
    {
        await _receiveValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && s.CompanyId == companyId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Purchasing.Supplier), request.SupplierId);
        }

        PurchaseOrder? purchaseOrder = null;
        if (request.PurchaseOrderId.HasValue)
        {
            purchaseOrder = await _db.PurchaseOrders
                .Include(po => po.Lines)
                .FirstOrDefaultAsync(po => po.Id == request.PurchaseOrderId.Value && po.CompanyId == companyId, cancellationToken);

            if (purchaseOrder is null)
            {
                throw new NotFoundException(nameof(PurchaseOrder), request.PurchaseOrderId.Value);
            }
        }

        var sequence = await _db.GoodsReceivedNotes.CountAsync(g => g.BranchId == branchId, cancellationToken) + 1;
        var grn = new GoodsReceivedNote
        {
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = request.SupplierId,
            PurchaseOrderId = request.PurchaseOrderId,
            GrnNumber = $"GRN-{branchId}-{sequence:D6}",
            ReceivedDate = DateTime.UtcNow,
            ReceivedByUserId = receivedByUserId,
        };

        var products = await _db.Products
            .Where(p => p.CompanyId == companyId && request.Lines.Select(l => l.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var line in request.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new NotFoundException("Product", line.ProductId);
            }

            var poLine = purchaseOrder?.Lines.FirstOrDefault(l => l.ProductId == line.ProductId);

            grn.Lines.Add(new GoodsReceivedNoteLine
            {
                PurchaseOrderLineId = poLine?.Id,
                ProductId = line.ProductId,
                QuantityReceived = line.QuantityReceived,
                UnitCost = line.UnitCost,
                BatchNumber = line.BatchNumber,
                ExpiryDate = line.ExpiryDate,
            });

            if (poLine is not null)
            {
                poLine.QuantityReceived += line.QuantityReceived;
            }

            if (product.TrackBatches || product.TrackExpiry)
            {
                _db.ProductBatches.Add(new ProductBatch
                {
                    CompanyId = companyId,
                    BranchId = branchId,
                    ProductId = product.Id,
                    BatchNumber = line.BatchNumber ?? $"AUTO-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    ExpiryDate = line.ExpiryDate,
                    ReceivedDate = DateTime.UtcNow,
                    QuantityReceived = line.QuantityReceived,
                    QuantityRemaining = line.QuantityReceived,
                });
            }
        }

        _db.GoodsReceivedNotes.Add(grn);

        // Two SaveChangesAsync calls are needed: the ledger's ReferenceId must be the
        // GRN's real (database-generated) Id, which only exists after the first save.
        // Both are wrapped in one transaction so a failure posting stock rolls back the
        // GRN too, rather than leaving a GRN on record with no matching stock movement.
        await _db.ExecuteInTransactionAsync(async () =>
        {
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var line in request.Lines)
            {
                await _stockService.PostMovementAsync(
                    companyId, branchId, line.ProductId, StockMovementType.Purchase, line.QuantityReceived,
                    nameof(GoodsReceivedNote), grn.Id, receivedByUserId, cancellationToken);
            }

            if (purchaseOrder is not null && purchaseOrder.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered))
            {
                purchaseOrder.Status = PurchaseOrderStatus.Received;
            }
            else if (purchaseOrder is not null)
            {
                purchaseOrder.Status = PurchaseOrderStatus.PartiallyReceived;
            }

            await _db.SaveChangesAsync(cancellationToken);
        });

        return new GoodsReceivedNoteDto
        {
            Id = grn.Id,
            GrnNumber = grn.GrnNumber,
            SupplierId = grn.SupplierId,
            PurchaseOrderId = grn.PurchaseOrderId,
            ReceivedDate = grn.ReceivedDate,
            Lines = request.Lines,
        };
    }

    public async Task<IReadOnlyList<GoodsReceivedNoteDto>> GetGoodsReceivedNotesAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        var notes = await _db.GoodsReceivedNotes
            .Include(g => g.Lines)
            .Where(g => g.CompanyId == companyId && g.BranchId == branchId)
            .OrderByDescending(g => g.ReceivedDate)
            .ToListAsync(cancellationToken);

        return notes.Select(g => new GoodsReceivedNoteDto
        {
            Id = g.Id,
            GrnNumber = g.GrnNumber,
            SupplierId = g.SupplierId,
            PurchaseOrderId = g.PurchaseOrderId,
            ReceivedDate = g.ReceivedDate,
            Lines = g.Lines.Select(l => new ReceiveGoodsLineRequest
            {
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                ProductId = l.ProductId,
                QuantityReceived = l.QuantityReceived,
                UnitCost = l.UnitCost,
                BatchNumber = l.BatchNumber,
                ExpiryDate = l.ExpiryDate,
            }).ToList(),
        }).ToList();
    }

    private static PurchaseOrderDto ToDto(PurchaseOrder order) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        SupplierId = order.SupplierId,
        Status = order.Status.ToString(),
        OrderDate = order.OrderDate,
        ExpectedDate = order.ExpectedDate,
        Lines = order.Lines.Select(l => new PurchaseOrderLineDto
        {
            ProductId = l.ProductId,
            QuantityOrdered = l.QuantityOrdered,
            QuantityReceived = l.QuantityReceived,
            UnitCost = l.UnitCost,
        }).ToList(),
    };

    public async Task<PurchaseInvoiceDto> CreateInvoiceAsync(long companyId, long branchId, CreatePurchaseInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        await _invoiceValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && s.CompanyId == companyId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Purchasing.Supplier), request.SupplierId);
        }

        if (request.GoodsReceivedNoteId.HasValue &&
            !await _db.GoodsReceivedNotes.AnyAsync(g => g.Id == request.GoodsReceivedNoteId.Value && g.CompanyId == companyId, cancellationToken))
        {
            throw new NotFoundException(nameof(GoodsReceivedNote), request.GoodsReceivedNoteId.Value);
        }

        var invoice = new PurchaseInvoice
        {
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = request.SupplierId,
            GoodsReceivedNoteId = request.GoodsReceivedNoteId,
            SupplierInvoiceNumber = request.SupplierInvoiceNumber,
            InvoiceDate = request.InvoiceDate,
            SubTotal = request.SubTotal,
            TaxTotal = request.TaxTotal,
            GrandTotal = request.SubTotal + request.TaxTotal,
            AmountPaid = 0,
            Status = Domain.Purchasing.PurchaseInvoiceStatus.Unpaid,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.PurchaseInvoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(invoice);
    }

    public async Task<IReadOnlyList<PurchaseInvoiceDto>> GetInvoicesAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        return await _db.PurchaseInvoices
            .Where(i => i.CompanyId == companyId && i.BranchId == branchId)
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => ToDto(i))
            .ToListAsync(cancellationToken);
    }

    public async Task<PurchaseInvoiceDto> RecordPaymentAsync(long companyId, long invoiceId, long userId, RecordSupplierPaymentRequest request, CancellationToken cancellationToken = default)
    {
        await _paymentValidator.ValidateAndThrowAsync(request, cancellationToken);

        var invoice = await _db.PurchaseInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), invoiceId);

        if (invoice.Status == Domain.Purchasing.PurchaseInvoiceStatus.Paid)
        {
            throw new ConflictException($"Invoice {invoice.SupplierInvoiceNumber} is already fully paid.");
        }

        var remaining = invoice.GrandTotal - invoice.AmountPaid;
        if (request.Amount > remaining)
        {
            throw new PaymentException($"Payment of {request.Amount:0.00} exceeds the remaining balance of {remaining:0.00}.");
        }

        _db.SupplierPayments.Add(new Domain.Purchasing.SupplierPayment
        {
            CompanyId = companyId,
            SupplierId = invoice.SupplierId,
            PurchaseInvoiceId = invoice.Id,
            Amount = request.Amount,
            Method = request.Method,
            ReferenceNo = request.ReferenceNo,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        });

        invoice.AmountPaid += request.Amount;
        invoice.Status = invoice.AmountPaid >= invoice.GrandTotal
            ? Domain.Purchasing.PurchaseInvoiceStatus.Paid
            : Domain.Purchasing.PurchaseInvoiceStatus.PartiallyPaid;

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(invoice);
    }

    private static PurchaseInvoiceDto ToDto(PurchaseInvoice invoice) => new()
    {
        Id = invoice.Id,
        SupplierId = invoice.SupplierId,
        SupplierInvoiceNumber = invoice.SupplierInvoiceNumber,
        InvoiceDate = invoice.InvoiceDate,
        GrandTotal = invoice.GrandTotal,
        AmountPaid = invoice.AmountPaid,
        Status = invoice.Status.ToString(),
    };
}
