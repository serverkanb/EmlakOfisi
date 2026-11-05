using System.Security.Claims;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.WebUI.Extensions;

namespace EmlakOfisi.Tests;

public class UserAccessExtensionsTests
{
    private static ClaimsPrincipal User(int id, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Theory]
    [InlineData(RoleNames.Admin, true)]
    [InlineData(RoleNames.Manager, true)]
    [InlineData(RoleNames.Agent, false)]
    public void CanSeeAll_ByRole(string role, bool expected)
    {
        Assert.Equal(expected, User(5, role).CanSeeAll());
    }

    [Fact]
    public void CanSeeAll_UserWithoutRole_IsFalse()
    {
        Assert.False(User(5).CanSeeAll());
    }

    [Fact]
    public void GetUserId_ReadsNameIdentifier()
    {
        Assert.Equal(42, User(42, RoleNames.Agent).GetUserId());
    }

    [Fact]
    public void GetUserId_Anonymous_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ClaimsPrincipal(new ClaimsIdentity()).GetUserId());
    }

    [Fact]
    public void StaffRoles_ContainAllRoles()
    {
        Assert.Equal(RoleNames.All, RoleNames.Staff.Split(','));
    }
}
