using UniversalPOS.Application.Common.Interfaces;

namespace UniversalPOS.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
