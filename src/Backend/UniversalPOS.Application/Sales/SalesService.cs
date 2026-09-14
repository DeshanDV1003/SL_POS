using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Crm;
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
    private readonly IPromotionEngine _promotionEngine;
    private readonly ILoyaltyService _loyaltyService;
    private readonly IReadOnlyDictionary<Domain.Sales.PaymentMethod, IPaymentProvider> _paymentProviders;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateSaleRequest> _saleValidator;
    private readonly IValidator<CreateHeldBillRequest> _heldBillValidator;
    private readonly IValidator<VoidSaleRequest> _voidValidator;
    private readonly IValidator<RefundSaleRequest> _refundValidator;

    public SalesService(
        IApplicationDbContext db,
        IStockService stockService,
        IFiscalReportingProvider fiscalReportingProvider,
        IPromotionEngine promotionEngine,
        ILoyaltyService loyaltyService,
        IEnumerable<IPaymentProvider> paymentProviders,
        ICurrentUserService currentUser,
        IValidator<CreateSaleRequest> saleValidator,
        IValidator<CreateHeldBillRequest> heldBillValidator,
        IValidator<VoidSaleRequest> voidValidator,
        IValidator<RefundSaleRequest> refundValidator)
    {
        _db = db;
        _stockService = stockService;
        _fiscalReportingProvider = fiscalReportingProvider;
        _promotionEngine = promotionEngine;
        _loyaltyService = loyaltyService;
        _paymentProviders = paymentProviders.ToDictionary(p => p.SupportedMethod);
        _currentUser = currentUser;
        _saleValidator = saleValidator;
        _heldBillValidator = heldBillValidator;
        _voidValidator = voidValidator;
        _refundValidator = refundValidator;
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

        // Attaching the cashier's open shift is normally best-effort — a shift is not
        // required to complete a sale unless the branch has opted into
        // RequireOpenShiftForSale (off by default, see docs/project-state.md), in
        // which case checkout without one is rejected outright rather than silently
        // recording CashierShiftId as null.
        var openShiftId = await _db.CashierShifts
            .Where(s => s.TerminalId == request.TerminalId && s.CashierUserId == cashierUserId && s.Status == Domain.Cash.ShiftStatus.Open)
            .Select(s => (long?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (branch.RequireOpenShiftForSale && !openShiftId.HasValue)
        {
            throw new ConflictException("This branch requires an open cashier shift before a sale can be completed. Open a shift first.");
        }

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

            var unitPrice = product.SellingPrice;
            if (lineRequest.UnitPriceOverride.HasValue)
            {
                if (!_currentUser.HasPermission(Domain.Identity.PermissionCodes.SalesPriceOverride))
                {
                    throw new ForbiddenException("You do not have permission to override the selling price.");
                }
                if (product.MinSellingPrice.HasValue && lineRequest.UnitPriceOverride.Value < product.MinSellingPrice.Value)
                {
                    throw new ValidationFailedException(new Dictionary<string, string[]>
                    {
                        [$"lines[{product.Id}].unitPriceOverride"] = new[] { $"Cannot go below the minimum selling price of {product.MinSellingPrice.Value:0.00}." },
                    });
                }
                unitPrice = lineRequest.UnitPriceOverride.Value;
            }

            var grossAmount = Domain.Sales.Money.Round(lineRequest.Quantity * unitPrice);
            var effectiveDiscountPercentage = await _promotionEngine.GetEffectiveDiscountPercentageAsync(
                companyId, product.Id, product.CategoryId, lineRequest.Quantity, grossAmount, lineRequest.DiscountPercentage, cancellationToken);

            var calc = Domain.Sales.SaleLineCalculator.Calculate(new Domain.Sales.SaleLineInput(
                lineRequest.Quantity, unitPrice, effectiveDiscountPercentage,
                taxRate?.Percentage ?? 0m, taxRate?.IsInclusive ?? false));

            sale.Lines.Add(new SaleLine
            {
                ProductId = product.Id,
                Quantity = lineRequest.Quantity,
                UnitPrice = unitPrice,
                DiscountPercentage = effectiveDiscountPercentage,
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

        Coupon? coupon = null;
        decimal couponDiscountAmount = 0;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var code = request.CouponCode.Trim().ToUpperInvariant();
            var now = DateTime.UtcNow;
            coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Code == code, cancellationToken)
                ?? throw new NotFoundException(nameof(Coupon), code);

            if (!coupon.IsActive || (coupon.ExpiresAtUtc.HasValue && coupon.ExpiresAtUtc.Value <= now))
            {
                throw new ConflictException($"Coupon '{code}' is no longer valid.");
            }
            if (coupon.MaxRedemptions.HasValue && coupon.TimesRedeemed >= coupon.MaxRedemptions.Value)
            {
                throw new ConflictException($"Coupon '{code}' has reached its redemption limit.");
            }
            if (subTotal < coupon.MinSaleAmount)
            {
                throw new ConflictException($"Coupon '{code}' requires a subtotal of at least {coupon.MinSaleAmount:0.00}.");
            }

            // v1 simplification: a flat reduction to GrandTotal — like a manufacturer
            // coupon — rather than redistributing the discount across lines and
            // recomputing the tax base. See docs/project-state.md.
            couponDiscountAmount = coupon.DiscountType == PromotionDiscountType.Percentage
                ? Domain.Sales.Money.Round(grandTotal * coupon.DiscountValue / 100m)
                : Math.Min(coupon.DiscountValue, grandTotal);
            grandTotal = Domain.Sales.Money.Round(grandTotal - couponDiscountAmount);
        }

        sale.SubTotal = subTotal;
        sale.DiscountTotal = discountTotal;
        sale.TaxTotal = taxTotal;
        sale.ServiceChargeTotal = serviceChargeTotal;
        sale.CouponId = coupon?.Id;
        sale.CouponDiscountAmount = couponDiscountAmount;
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
        // never leave a half-created sale behind. LoyaltyPoints is handled entirely
        // here rather than via IPaymentProvider — it needs the sale's CustomerId, which
        // PaymentAuthorizationRequest doesn't carry, and the actual point deduction
        // must happen inside the same transaction as the sale (below), once sale.Id
        // exists for the ledger row to reference.
        var authorizedPayments = new List<(CreateSalePaymentRequest Request, string? ProviderReference)>();
        var pointsToRedeem = 0;
        foreach (var paymentRequest in request.Payments)
        {
            if (paymentRequest.Method == Domain.Sales.PaymentMethod.LoyaltyPoints)
            {
                if (!sale.CustomerId.HasValue)
                {
                    throw new ValidationFailedException(new Dictionary<string, string[]>
                    {
                        ["customerId"] = new[] { "A customer must be selected to pay with loyalty points." },
                    });
                }

                var (points, hasEnough) = await _loyaltyService.QuotePointsForAmountAsync(companyId, sale.CustomerId.Value, paymentRequest.Amount, cancellationToken);
                if (!hasEnough)
                {
                    throw new ConflictException("Customer does not have enough loyalty points to cover this payment amount.");
                }

                pointsToRedeem += points;
                authorizedPayments.Add((paymentRequest, "LOYALTY"));
                continue;
            }

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

            if (pointsToRedeem > 0)
            {
                await _loyaltyService.RedeemPointsForSaleAsync(companyId, sale.CustomerId!.Value, pointsToRedeem, sale.Id, cancellationToken);
            }

            if (sale.CustomerId.HasValue)
            {
                await _loyaltyService.EarnPointsForSaleAsync(companyId, sale.CustomerId.Value, sale.Id, sale.GrandTotal, cancellationToken);
            }

            if (coupon is not null)
            {
                coupon.TimesRedeemed += 1;
                await _db.SaveChangesAsync(cancellationToken);
            }
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

    public async Task<SaleReceiptDto> RefundSaleAsync(long companyId, long branchId, long? terminalId, long userId, long originalSaleId, RefundSaleRequest request, CancellationToken cancellationToken = default)
    {
        await _refundValidator.ValidateAndThrowAsync(request, cancellationToken);

        var original = await _db.SaleHeaders.Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == originalSaleId && s.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(SaleHeader), originalSaleId);

        if (original.Status != SaleStatus.Completed)
        {
            throw new ConflictException($"Sale {original.InvoiceNumber} is not in a refundable state ({original.Status}).");
        }

        // A sale can be refunded across multiple partial requests, but never more than
        // was originally sold — sum whatever prior refunds already took for each product.
        var alreadyRefunded = await _db.SaleLines
            .Where(l => _db.SaleHeaders.Any(h => h.Id == l.SaleHeaderId && h.OriginalSaleHeaderId == originalSaleId && h.Status == SaleStatus.Refunded))
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);

        var refund = new SaleHeader
        {
            CompanyId = companyId,
            BranchId = branchId,
            TerminalId = terminalId ?? original.TerminalId,
            CashierUserId = userId,
            CustomerId = original.CustomerId,
            Status = SaleStatus.Refunded,
            OriginalSaleHeaderId = original.Id,
            RefundReason = request.Reason,
            CreatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
        };

        decimal subTotal = 0, discountTotal = 0, taxTotal = 0, lineTotalsSum = 0;

        foreach (var lineRequest in request.Lines)
        {
            var originalLine = original.Lines.FirstOrDefault(l => l.ProductId == lineRequest.ProductId)
                ?? throw new ValidationFailedException(new Dictionary<string, string[]>
                {
                    ["lines"] = new[] { $"Product {lineRequest.ProductId} was not part of the original sale." },
                });

            var refundedSoFar = alreadyRefunded.GetValueOrDefault(lineRequest.ProductId);
            var remaining = originalLine.Quantity - refundedSoFar;
            if (lineRequest.Quantity > remaining)
            {
                throw new ConflictException($"Cannot refund {lineRequest.Quantity} of product {lineRequest.ProductId}; only {remaining} remains refundable.");
            }

            // Prorate the ORIGINAL line's already-computed discount/tax/total by the
            // fraction of the line being refunded, rather than recomputing from
            // scratch — that reflects exactly what the customer was actually charged
            // (today's price/promotion/tax settings may have since changed) and
            // sidesteps needing to know whether the original tax was inclusive or
            // exclusive, since LineTotal/LineDiscountAmount/LineTaxAmount already
            // bake that in.
            var proportion = lineRequest.Quantity / originalLine.Quantity;
            var grossAmount = Domain.Sales.Money.Round(originalLine.Quantity * originalLine.UnitPrice * proportion);
            var discountAmount = Domain.Sales.Money.Round(originalLine.LineDiscountAmount * proportion);
            var taxAmount = Domain.Sales.Money.Round(originalLine.LineTaxAmount * proportion);
            var lineTotal = Domain.Sales.Money.Round(originalLine.LineTotal * proportion);

            refund.Lines.Add(new SaleLine
            {
                ProductId = lineRequest.ProductId,
                Quantity = lineRequest.Quantity,
                UnitPrice = originalLine.UnitPrice,
                DiscountPercentage = originalLine.DiscountPercentage,
                TaxRatePercentage = originalLine.TaxRatePercentage,
                LineDiscountAmount = discountAmount,
                LineTaxAmount = taxAmount,
                LineTotal = lineTotal,
            });

            subTotal += grossAmount;
            discountTotal += discountAmount;
            taxTotal += taxAmount;
            lineTotalsSum += lineTotal;
        }

        refund.SubTotal = subTotal;
        refund.DiscountTotal = discountTotal;
        refund.TaxTotal = taxTotal;
        refund.GrandTotal = Domain.Sales.Money.Round(lineTotalsSum);

        var paymentsTotal = Domain.Sales.Money.Round(request.Payments.Sum(p => p.Amount));
        if (paymentsTotal != refund.GrandTotal)
        {
            throw new PaymentException($"Refund payments total {paymentsTotal:0.00} but the refund amount is {refund.GrandTotal:0.00}.");
        }

        foreach (var payment in request.Payments)
        {
            refund.Payments.Add(new SalePayment { Method = payment.Method, Amount = payment.Amount, ProviderStatus = "Refunded" });
        }

        var sequence = await _db.SaleHeaders.CountAsync(s => s.BranchId == branchId && s.Status == SaleStatus.Refunded, cancellationToken) + 1;
        refund.InvoiceNumber = $"CN-{branchId}-{sequence:D6}";

        await _db.ExecuteInTransactionAsync(async () =>
        {
            _db.SaleHeaders.Add(refund);
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var line in refund.Lines)
            {
                await _stockService.PostMovementAsync(
                    companyId, branchId, line.ProductId, StockMovementType.SaleReturn, line.Quantity,
                    nameof(SaleHeader), refund.Id, userId, cancellationToken);
            }

            _db.AuditLogs.Add(new AuditLog
            {
                CompanyId = companyId,
                BranchId = branchId,
                TerminalId = terminalId,
                UserId = userId,
                ActionCode = "Sale.Refund",
                EntityType = nameof(SaleHeader),
                EntityId = original.Id.ToString(),
                NewValueJson = JsonSerializer.Serialize(new { RefundSaleHeaderId = refund.Id, refund.GrandTotal, request.Reason }),
                CreatedAtUtc = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync(cancellationToken);
        });

        return await BuildReceiptAsync(refund.Id, cancellationToken);
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
        CouponDiscountAmount = sale.CouponDiscountAmount,
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
