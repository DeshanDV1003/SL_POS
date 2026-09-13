using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Crm.Dtos;
using UniversalPOS.Domain.Crm;

namespace UniversalPOS.Application.Crm;

public class CustomerService : ICustomerService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateCustomerGroupRequest> _groupValidator;
    private readonly IValidator<CreateCustomerRequest> _customerValidator;

    public CustomerService(
        IApplicationDbContext db,
        IValidator<CreateCustomerGroupRequest> groupValidator,
        IValidator<CreateCustomerRequest> customerValidator)
    {
        _db = db;
        _groupValidator = groupValidator;
        _customerValidator = customerValidator;
    }

    public async Task<IReadOnlyList<CustomerGroupDto>> GetCustomerGroupsAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _db.CustomerGroups
            .Where(g => g.CompanyId == companyId)
            .OrderBy(g => g.Name)
            .Select(g => new CustomerGroupDto { Id = g.Id, Name = g.Name, DefaultDiscountPercentage = g.DefaultDiscountPercentage })
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerGroupDto> CreateCustomerGroupAsync(long companyId, CreateCustomerGroupRequest request, CancellationToken cancellationToken = default)
    {
        await _groupValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await _db.CustomerGroups.AnyAsync(g => g.CompanyId == companyId && g.Name == request.Name, cancellationToken))
        {
            throw new ConflictException($"A customer group named '{request.Name}' already exists.");
        }

        var group = new CustomerGroup
        {
            CompanyId = companyId,
            Name = request.Name,
            DefaultDiscountPercentage = request.DefaultDiscountPercentage,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.CustomerGroups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);

        return new CustomerGroupDto { Id = group.Id, Name = group.Name, DefaultDiscountPercentage = group.DefaultDiscountPercentage };
    }

    public async Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(long companyId, string? search, CancellationToken cancellationToken = default)
    {
        var query = _db.Customers.Where(c => c.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) || (c.Phone != null && c.Phone.Contains(search)));
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CustomerDto
            {
                Id = c.Id,
                CustomerGroupId = c.CustomerGroupId,
                Name = c.Name,
                Phone = c.Phone,
                Email = c.Email,
                CreditLimit = c.CreditLimit,
                OutstandingBalance = c.OutstandingBalance,
                LoyaltyPointsBalance = c.LoyaltyPointsBalance,
                IsActive = c.IsActive,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerDto> CreateCustomerAsync(long companyId, CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        await _customerValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (request.CustomerGroupId.HasValue &&
            !await _db.CustomerGroups.AnyAsync(g => g.Id == request.CustomerGroupId.Value && g.CompanyId == companyId, cancellationToken))
        {
            throw new NotFoundException(nameof(CustomerGroup), request.CustomerGroupId.Value);
        }

        var customer = new Customer
        {
            CompanyId = companyId,
            CustomerGroupId = request.CustomerGroupId,
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            TaxRegistrationNo = request.TaxRegistrationNo,
            CreditLimit = request.CreditLimit,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);

        return new CustomerDto
        {
            Id = customer.Id,
            CustomerGroupId = customer.CustomerGroupId,
            Name = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            CreditLimit = customer.CreditLimit,
            OutstandingBalance = customer.OutstandingBalance,
            LoyaltyPointsBalance = customer.LoyaltyPointsBalance,
            IsActive = customer.IsActive,
        };
    }
}
