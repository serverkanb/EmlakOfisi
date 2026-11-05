using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.Tests;

// CLAUDE.md kural 2: müşteri ve portföy kayıtlarında varsayılan görünürlük "Sadece Ben".
public class EntityDefaultsTests
{
    [Fact]
    public void NewContact_IsOnlyMe_AndNotDeleted()
    {
        var contact = new Contact();

        Assert.Equal(Visibility.OnlyMe, contact.Visibility);
        Assert.False(contact.IsDeleted);
    }

    [Fact]
    public void NewProperty_IsOnlyMe_ActiveAndNotDeleted()
    {
        var property = new Property();

        Assert.Equal(Visibility.OnlyMe, property.Visibility);
        Assert.Equal(PropertyStatus.Active, property.Status);
        Assert.False(property.IsDeleted);
    }

    [Fact]
    public void VisibilityOnlyMe_IsZero()
    {
        // Veritabanında int kolonu varsayılan 0 olur; 0'ın "Sadece Ben" olması güvenli taraftır.
        Assert.Equal(0, (int)Visibility.OnlyMe);
    }

    [Fact]
    public void NewAppUser_IsActive()
    {
        Assert.True(new AppUser().IsActive);
    }
}
