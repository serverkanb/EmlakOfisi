using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EmlakOfisi.Tests;

// EF modelini veritabanına bağlanmadan kurar ve kuralları kontrol eder.
// (Model kurulurken ilişki hatası olursa bu testler zaten patlar.)
public class AppDbContextModelTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=model-test-only;Database=none") // bağlantı açılmaz
            .Options;

        using var context = new AppDbContext(options);
        return context.Model;
    }

    [Fact]
    public void AllForeignKeys_OnOurEntities_AreRestrict()
    {
        var model = BuildModel();
        var ourNamespace = typeof(Contact).Namespace;

        var cascading = model.GetEntityTypes()
            .Where(e => e.ClrType.Namespace == ourNamespace)
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.DeleteBehavior != DeleteBehavior.Restrict)
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name} -> {fk.PrincipalEntityType.ClrType.Name}")
            .ToList();

        Assert.Empty(cascading);
    }

    [Theory]
    [InlineData(typeof(Property), nameof(Property.Price), "decimal(18,2)")]
    [InlineData(typeof(CustomerRequest), nameof(CustomerRequest.MinPrice), "decimal(18,2)")]
    [InlineData(typeof(CustomerRequest), nameof(CustomerRequest.MaxPrice), "decimal(18,2)")]
    [InlineData(typeof(Transaction), nameof(Transaction.OfferPrice), "decimal(18,2)")]
    [InlineData(typeof(Transaction), nameof(Transaction.AgreedPrice), "decimal(18,2)")]
    [InlineData(typeof(Transaction), nameof(Transaction.CommissionAmount), "decimal(18,2)")]
    [InlineData(typeof(Transaction), nameof(Transaction.CommissionRate), "decimal(5,2)")]
    public void MoneyColumns_HaveExpectedSqlType(Type entity, string propertyName, string expected)
    {
        var property = BuildModel().FindEntityType(entity)!.FindProperty(propertyName)!;

        Assert.Equal(expected, property.GetColumnType());
    }

    [Fact]
    public void Property_HasSeparateForeignKeys_ForAgentAndCreator()
    {
        var foreignKeys = BuildModel().FindEntityType(typeof(Property))!
            .GetForeignKeys()
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(AppUser))
            .Select(fk => fk.Properties.Single().Name)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[] { nameof(Property.AgentUserId), nameof(Property.CreatedByUserId) }, foreignKeys);
    }
}
