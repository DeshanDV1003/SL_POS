using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Domain.Purchasing;

namespace UniversalPOS.Application.Purchasing;

public class SupplierService : ISupplierService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateSupplierRequest> _validator;

    public SupplierService(IApplicationDbContext db, IValidator<CreateSupplierRequest> validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _db.Suppliers
            .Where(s => s.CompanyId == companyId)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto { Id = s.Id, Name = s.Name, ContactPerson = s.ContactPerson, Phone = s.Phone, Email = s.Email, IsActive = s.IsActive })
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierDto> CreateSupplierAsync(long companyId, CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        if (await _db.Suppliers.AnyAsync(s => s.CompanyId == companyId && s.Name == request.Name, cancellationToken))
        {
            throw new ConflictException($"A supplier named '{request.Name}' already exists.");
        }

        var supplier = new Supplier
        {
            CompanyId = companyId,
            Name = request.Name,
            ContactPerson = request.ContactPerson,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            TaxRegistrationNo = request.TaxRegistrationNo,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(cancellationToken);

        return new SupplierDto { Id = supplier.Id, Name = supplier.Name, ContactPerson = supplier.ContactPerson, Phone = supplier.Phone, Email = supplier.Email, IsActive = supplier.IsActive };
    }
}
