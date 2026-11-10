using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockFlow.Infrastructure.Identity;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Tests;

public class StockFlowIdentityDbContextModelTests
{
    private static StockFlowIdentityDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<StockFlowIdentityDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=unused_identity_model_test")
                .Options;

        return new StockFlowIdentityDbContext(options);
    }

    [Fact]
    public void Model_UsesIdentitySchema()
    {
        using var context = CreateContext();

        Assert.Equal("identity", context.Model.GetDefaultSchema());

        Assert.All(context.Model.GetEntityTypes(), entity =>
            Assert.Equal("identity", entity.GetSchema()));
    }

    [Fact]
    public void Model_UsesGuidUserAndRoleKeys()
    {
        using var context = CreateContext();

        var user = context.Model.FindEntityType(typeof(ApplicationUser));
        var role = context.Model.FindEntityType(typeof(IdentityRole<Guid>));

        Assert.NotNull(user);
        Assert.NotNull(role);

        var userKey = Assert.Single(user.FindPrimaryKey()!.Properties);
        var roleKey = Assert.Single(role.FindPrimaryKey()!.Properties);

        Assert.Equal("Id", userKey.Name);
        Assert.Equal(typeof(Guid), userKey.ClrType);
        Assert.Equal("Id", roleKey.Name);
        Assert.Equal(typeof(Guid), roleKey.ClrType);
    }

    [Theory]
    [InlineData(nameof(ApplicationUser.DisplayName), 200)]
    [InlineData(nameof(ApplicationUser.Email), 256)]
    [InlineData(nameof(ApplicationUser.NormalizedEmail), 256)]
    public void Model_RequiresUserFieldsWithLengthLimits(
        string field,
        int maximumLength)
    {
        using var context = CreateContext();

        var user = context.Model.FindEntityType(typeof(ApplicationUser));
        Assert.NotNull(user);

        var property = user.FindProperty(field);
        Assert.NotNull(property);

        Assert.False(property.IsNullable);
        Assert.Equal(maximumLength, property.GetMaxLength());
    }

    [Fact]
    public void Model_EnforcesUniqueNormalizedEmail()
    {
        using var context = CreateContext();

        var user = context.Model.FindEntityType(typeof(ApplicationUser));
        Assert.NotNull(user);

        var index = Assert.Single(user.GetIndexes(), candidate =>
            candidate.Properties.Count == 1 &&
            candidate.Properties[0].Name ==
                nameof(ApplicationUser.NormalizedEmail));

        Assert.True(index.IsUnique);
        Assert.Equal("EmailIndex", index.GetDatabaseName());
        Assert.Null(index.GetFilter());
    }

    [Fact]
    public void Model_ConfiguresUserRoleMembership()
    {
        using var context = CreateContext();

        var membership = context.Model.FindEntityType(
            typeof(IdentityUserRole<Guid>));

        Assert.NotNull(membership);

        Assert.Equal(
            new[] { "UserId", "RoleId" },
            membership.FindPrimaryKey()!.Properties
                .Select(property => property.Name)
                .ToArray());

        Assert.Contains(membership.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType ==
                typeof(ApplicationUser));

        Assert.Contains(membership.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType ==
                typeof(IdentityRole<Guid>));
    }

    [Fact]
    public void Models_KeepBusinessAndIdentityEntitiesSeparate()
    {
        using var identity = CreateContext();

        Assert.DoesNotContain(
            identity.Model.GetEntityTypes(),
            entity => entity.ClrType.Namespace ==
                "StockFlow.Domain.Entities");

        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_business_model_test")
            .Options;

        using var business = new StockFlowDbContext(options);

        Assert.Null(
            business.Model.FindEntityType(typeof(ApplicationUser)));
        Assert.Null(
            business.Model.FindEntityType(typeof(IdentityRole<Guid>)));
    }
}