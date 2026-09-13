using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Inventory;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Auditing;
using UniversalPOS.Domain.Catalog;
using UniversalPOS.Domain.Inventory;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Sales;

public class SalesService : ISalesService
{
    private readonly IApplicationDbContext _db;
    private readonly IStockService _stockService;
    private readonly IFiscalReportingProvider _fiscalReportingProvider;
    private readonly IReadOnlyDictionary<Domain.Sales.PaymentMethod, IPaymentProvider> _paymentProviders;
    private readonly IValidator<CreateSaleRequest> _saleValidator;
    private readonly IValidator<CreateHeldBillRequest> _heldBillValidator;
    private readonly IValidator<VoidSaleRequest> _voidValidator;

    public SalesService(
        IApplicationDbContext db,
        IStockService stockService,
        IFiscalReportingProvider fiscalReportingProvider,
        IEnumerable<IPaymentProvider> paymentProviders,
        IValidator<CreateSaleRequest> saleValidator,
        IValidator<CreateHeldBillRequest> heldBillValidator,
        IValidator<VoidSaleRequest> voidValidator)
    {
        _db = db;
        _stockService = stockService;
        _fiscalReportingProvider = fiscalReportingProvider;
        _paymentProviders = paymentProviders.ToDictionary(p => p.SupportedMethod);
        _saleValidator = saleValidator;
        _heldBillValidator = heldBillValidator;
        _voidValidator = voidValidator;
    }

    public async Task<SaleReceiptDto> CheckoutAsync(long companyId, long branchId, long cashierUserId, CreateSaleRequest request, CancellationToken cancellationToken = default)
    {
        await _saleValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!string.IsNullOrEmpty(request.ClientIdempotencyKey))
        {
            var existing = await _db.SaleHeaders.FirstOrDefaultAsync(s => s.ClientIdempotencyKey == request.ClientIdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return await BuildReceiptAsync(existing.Id, cancellationToken);
            }
        }

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Branch", branchId);

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => p.CompanyId == companyId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        if (products.Count != productIds.Count)
        {
            throw new NotFoundException("Product", string.Join(",", productIds.Except(products.Keys)));
        }
        if (products.Values.Any(p => !p.IsActive))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["lines"] = new[] { "One or more products are inactive and cannot be sold." },
            });
        }

        var categoryIds = products.Values.Where(p => p.CategoryId.HasValue).Select(p => p.CategoryId!.Value).Distinct().ToList();
        var categoryDefaultTax = await _db.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.DefaultTaxRateId, cancellationToken);

        var taxRateIds = products.Values.Select(p => p.TaxRateId)
            .Concat(categoryDefaultTax.Values)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var taxRates = await _db.TaxRates.Where(t => taxRateIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);

        // Attaching the cashier's open shift is best-effort, not mandatory: a shift is
        // not currently required to complete a sale (see docs/project-state.md), so a
        // sale with no open shift simply reports with CashierShiftId null rather than
        // being blocked.
        var openShiftId = await _db.CashierShifts
            .Where(s => s.TerminalId == request.TerminalId && s.CashierUserId == cashierUserId && s.Status == Domain.Cash.ShiftStatus.Open)
            .Select(s => (long?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var sale = new SaleHeader
        {
            CompanyId = companyId,
            BranchId = branchId,
            TerminalId = request.TerminalId,
            CashierUserId = cashierUserId,
            CashierShiftId = openShiftId,
            CustomerId = request.CustomerId,
            InvoiceMode = request.InvoiceMode,
            PurchaserTin = request.PurchaserTin,
            PurchaserName = request.PurchaserName,
            PurchaserAddress = request.PurchaserAddress,
            ClientIdempotencyKey = request.ClientIdempotencyKey,
            Status = SaleStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow,
        };

        decimal subTotal = 0, discountTotal = 0, taxTotal = 0, lineTotalsSum = 0;

        foreach (var lineRequest in request.Lines)
        {
            var product = products[lineRequest.ProductId];
            var effectiveTaxRateId = product.TaxRateId ?? (product.CategoryId.HasValue ? categoryDefaultTax.GetValueOrDefault(product.CategoryId.Value) : null);
            TaxRate? taxRate = effectiveTaxRateId.HasValue ? taxRates.GetValueOrDefault(effectiveTaxRateId.Value) : null;

            var calc = Domain.Sales.SaleLineCalculator.Calculate(new Domain.Sales.SaleLineInput(
                lineRequest.Quantity, product.SellingPrice, lineRequest.DiscountPercentage,
                taxRate?.Percentage ?? 0m, taxRate?.IsInclusive ?? false));

            sale.Lines.Add(new SaleLine
            {
                ProductId = product.Id,
                Quantity = lineRequest.Quantity,
                UnitPrice = product.SellingPrice,
                DiscountPercentage = lineRequest.DiscountPercentage,
                TaxRatePercentage = taxRate?.Percentage ?? 0m,
                LineDiscountAmount = calc.DiscountAmount,
                LineTaxAmount = calc.TaxAmount,
                LineTotal = calc.LineTotal,
            });

            subTotal += calc.GrossAmount;
            discountTotal += calc.DiscountAmount;
            taxTotal += calc.TaxAmount;
            lineTotalsSum += calc.LineTotal;
        }

        var serviceChargeTotal = Domain.Sales.Money.Round((subTotal - discountTotal) * branch.ServiceChargeRate);
        var grandTotal = Domain.Sales.Money.Round(lineTotalsSum + serviceChargeTotal);

        sale.SubTotal = subTotal;
        sale.DiscountTotal = discountTotal;
        sale.TaxTotal = taxTotal;
        sale.ServiceChargeTotal = serviceChargeTotal;
        sale.GrandTotal = grandTotal;

        Domain.Sales.PaymentAllocationResult allocation;
        try
        {
            allocation = Domain.Sales.PaymentAllocator.Allocate(
                grandTotal,
                request.Payments.Select(p => new Domain.Sales.PaymentLineInput(p.Method, p.Amount)).ToList());
        }
        catch (Domain.Sales.PaymentValidationException ex)
        {
            throw new PaymentException(ex.Message);
        }

        if (!allocation.IsFullyPaid)
        {
            throw new PaymentException($"Payment total {allocation.TotalTendered:0.00} is less than the sale total {grandTotal:0.00}.");
        }

        // Authorize every non-cash payment BEFORE any database write — a decline must
        // never leave a half-created sale behind.
        var authorizedPayments = new List<(CreateSalePaymentRequest Request, string? ProviderReference)>();
        foreach (var paymentRequest in request.Payments)
        {
            if (!_paymentProviders.TryGetValue(paymentRequest.Method, out var provider))
            {
                throw new ConflictException($"Payment method '{paymentRequest.Method}' has no configured provider yet.");
            }

            var result = await provider.AuthorizeAsync(new PaymentAuthorizationRequest(companyId, branchId, paymentRequest.Amount, paymentRequest.InstrumentToken), cancellationToken);
            if (!result.Success)
            {
                throw new PaymentException(result.FailureReason ?? "Payment was declined.");
            }

            authorizedPayments.Add((paymentRequest, result.ProviderReference));
        }

        foreach (var (paymentRequest, providerReference) in authorizedPayments)
        {
            sale.Payments.Add(new SalePayment
            {
                Method = paymentRequest.Method,
                Amount = paymentRequest.Amount,
                ProviderReference = providerReference,
                ProviderStatus = "Approved",
            });
        }

        var sequence = await _db.SaleHeaders.CountAsync(s => s.BranchId == branchId && s.Status == SaleStatus.Completed, cancellationToken) + 1;
        sale.InvoiceNumber = $"INV-{branchId}-{sequence:D6}";
        sale.CompletedAtUtc = DateTime.UtcNow;

        await _db.ExecuteInTransactionAsync(async () =>
        {
            _db.SaleHeaders.Add(sale);
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var line in sale.Lines)
            {
                await _stockService.PostMovementAsync(
                    companyId, branchId, line.ProductId, StockMovementType.Sale, -line.Quantity,
                    nameof(SaleHeader), sale.Id, cashierUserId, cancellationToken);
            }

            var fiscalResult = await _fiscalReportingProvider.TransmitAsync(
                new FiscalTransmissionRequest(companyId, branchId, sale.Id, JsonSerializer.Serialize(new { sale.InvoiceNumber, sale.GrandTotal, sale.CompletedAtUtc })),
                cancellationToken);

            _db.FiscalTransmissions.Add(new Domain.Fiscal.FiscalTransmission
            {
                CompanyId = companyId,
                BranchId = branchId,
                SaleHeaderId = sale.Id,
                Status = fiscalResult.Success ? Domain.Fiscal.FiscalTransmissionStatus.Acknowledged : Domain.Fiscal.FiscalTransmissionStatus.Failed,
                Payload = JsonSerializer.Serialize(new { sale.InvoiceNumber, sale.GrandTotal }),
                ProviderResponse = fiscalResult.ProviderResponse,
                AttemptCount = 1,
                LastAttemptAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync(cancellationToken);
        });

        var dto = ToReceiptDto(sale, products);
        dto.ChangeDue = allocation.ChangeDue;
        return dto;
    }

    public async Task<SaleReceiptDto> GetSaleAsync(long companyId, long saleId, CancellationToken cancellationToken = default)
        => await BuildReceiptAsync(saleId, cancellationToken, companyId);

    public async Task<IReadOnlyList<SaleReceiptDto>> GetSalesAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        var saleIds = await _db.SaleHeaders
            .Where(s => s.CompanyId == companyId && s.BranchId == branchId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var results = new List<SaleReceiptDto>();
        foreach (var id in saleIds)
        {
            results.Add(await BuildReceiptAsync(id, cancellationToken));
        }
        return results;
    }

    public async Task<SaleReceiptDto> VoidSaleAsync(long companyId, long branchId, long? terminalId, long userId, long saleId, VoidSaleRequest request, CancellationToken cancellationToken = default)
    {
        await _voidValidator.ValidateAndThrowAsync(request, cancellationToken);

        var sale = await _db.SaleHeaders
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == saleId && s.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(SaleHeader), saleId);

        if (sale.Status != SaleStatus.Completed)
        {
            throw new ConflictException($"Sale {sale.InvoiceNumber} is not in a voidable state ({sale.Status}).");
        }

        foreach (var line in sale.Lines)
        {
            await _stockService.PostMovementAsync(
                companyId, sale.BranchId, line.ProductId, StockMovementType.SaleReturn, line.Quantity,
                nameof(SaleHeader), sale.Id, userId, cancellationToken);
        }

        sale.Status = SaleStatus.Voided;
        sale.VoidReason = request.Reason;

        _db.AuditLogs.Add(new AuditLog
        {
            CompanyId = companyId,
            BranchId = branchId,
            TerminalId = terminalId,
            UserId = userId,
            ActionCode = "Sale.Void",
            EntityType = nameof(SaleHeader),
            EntityId = sale.Id.ToString(),
            OldValueJson = JsonSerializer.Serialize(new { Status = nameof(SaleStatus.Completed) }),
            NewValueJson = JsonSerializer.Serialize(new { Status = nameof(SaleStatus.Voided), request.Reason }),
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await BuildReceiptAsync(sale.Id, cancellationToken);
    }

    public async Task<HeldBillDto> HoldAsync(long companyId, long branchId, long cashierUserId, CreateHeldBillRequest request, CancellationToken cancellationToken = default)
    {
        await _heldBillValidator.ValidateAndThrowAsync(request, cancellationToken);

        var heldBill = new Domain.Sales.HeldBill
        {
            CompanyId = companyId,
            BranchId = branchId,
            TerminalId = request.TerminalId,
            CashierUserId = cashierUserId,
            CustomerId = request.CustomerId,
            Notes = request.Notes,
            CreatedAtUtc = DateTime.UtcNow,
        };

        foreach (var line in request.Lines)
        {
            heldBill.Lines.Add(new Domain.Sales.HeldBillLine { ProductId = line.ProductId, Quantity = line.Quantity, DiscountPercentage = line.DiscountPercentage });
        }

        _db.HeldBills.Add(heldBill);
        await _db.SaveChangesAsync(cancellationToken);

        return ToHeldBillDto(heldBill);
    }

    public async Task<IReadOnlyList<HeldBillDto>> GetHeldBillsAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        var bills = await _db.HeldBills
            .Include(h => h.Lines)
            .Where(h => h.CompanyId == companyId && h.BranchId == branchId)
            .OrderBy(h => h.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return bills.Select(ToHeldBillDto).ToList();
    }

    public async Task<HeldBillDto> RecallAsync(long companyId, long heldBillId, CancellationToken cancellationToken = default)
    {
        var bill = await _db.HeldBills
            .Include(h => h.Lines)
            .FirstOrDefaultAsync(h => h.Id == heldBillId && h.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Sales.HeldBill), heldBillId);

        return ToHeldBillDto(bill);
    }

    public async Task DeleteHeldBillAsync(long companyId, long heldBillId, CancellationToken cancellationToken = default)
    {
        var bill = await _db.HeldBills.FirstOrDefaultAsync(h => h.Id == heldBillId && h.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Sales.HeldBill), heldBillId);

        _db.HeldBills.Remove(bill);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<SaleReceiptDto> BuildReceiptAsync(long saleId, CancellationToken cancellationToken, long? companyId = null)
    {
        var query = _db.SaleHeaders.Include(s => s.Lines).Include(s => s.Payments).Where(s => s.Id == saleId);
        if (companyId.HasValue) query = query.Where(s => s.CompanyId == companyId.Value);

        var sale = await query.FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException(nameof(SaleHeader), saleId);

        var productIds = sale.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var totalTendered = sale.Payments.Sum(p => p.Amount);
        var dto = ToReceiptDto(sale, products);
        dto.ChangeDue = Domain.Sales.Money.Round(Math.Max(0, totalTendered - sale.GrandTotal));
        return dto;
    }

    private static SaleReceiptDto ToReceiptDto(SaleHeader sale, Dictionary<long, Product> products) => new()
    {
        Id = sale.Id,
        InvoiceNumber = sale.InvoiceNumber,
        Status = sale.Status.ToString(),
        SubTotal = sale.SubTotal,
        DiscountTotal = sale.DiscountTotal,
        TaxTotal = sale.TaxTotal,
        ServiceChargeTotal = sale.ServiceChargeTotal,
        GrandTotal = sale.GrandTotal,
        CompletedAtUtc = sale.CompletedAtUtc ?? sale.CreatedAtUtc,
        Lines = sale.Lines.Select(l => new SaleLineDto
        {
            ProductId = l.ProductId,
            ProductName = products.TryGetValue(l.ProductId, out var p) ? p.Name : "(unknown product)",
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            DiscountPercentage = l.DiscountPercentage,
            LineDiscountAmount = l.LineDiscountAmount,
            LineTaxAmount = l.LineTaxAmount,
            LineTotal = l.LineTotal,
        }).ToList(),
        Payments = sale.Payments.Select(p => new SalePaymentDto { Method = p.Method.ToString(), Amount = p.Amount, ProviderReference = p.ProviderReference }).ToList(),
    };

    private static HeldBillDto ToHeldBillDto(Domain.Sales.HeldBill bill) => new()
    {
        Id = bill.Id,
        Notes = bill.Notes,
        CreatedAtUtc = bill.CreatedAtUtc,
        Lines = bill.Lines.Select(l => new CreateSaleLineRequest { ProductId = l.ProductId, Quantity = l.Quantity, DiscountPercentage = l.DiscountPercentage }).ToList(),
    };
}
