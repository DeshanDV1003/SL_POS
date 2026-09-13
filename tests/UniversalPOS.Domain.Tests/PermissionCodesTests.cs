using FluentAssertions;
using UniversalPOS.Domain.Identity;
using Xunit;

namespace UniversalPOS.Domain.Tests;

public class PermissionCodesTests
{
    [Fact]
    public void All_ContainsNoDuplicateCodes()
    {
        var codes = PermissionCodes.All.Select(p => p.Code).ToList();

        codes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_EveryEntryHasCategoryAndDescription()
    {
        PermissionCodes.All.Should().OnlyContain(p =>
            !string.IsNullOrWhiteSpace(p.Code) &&
            !string.IsNullOrWhiteSpace(p.Category) &&
            !string.IsNullOrWhiteSpace(p.Description));
    }
}
