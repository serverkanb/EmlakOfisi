using System.ComponentModel.DataAnnotations;
using System.Reflection;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace EmlakOfisi.Tests;

// Formlardaki İngilizce varsayılan mesajlar Türkçe olmalı (binder ve mesajı yazılmamış doğrulama kuralları).
public class TurkishValidationMessagesTests
{
    private static MvcOptions Options()
    {
        var options = new MvcOptions();
        TurkishValidationMessages.Apply(options);
        return options;
    }

    [Fact]
    public void BinderMessages_AreTurkish()
    {
        var p = Options().ModelBindingMessageProvider;
        var messages = new[]
        {
            p.AttemptedValueIsInvalidAccessor("abc", "Kat"),
            p.MissingBindRequiredValueAccessor("Kat"),
            p.MissingKeyOrValueAccessor(),
            p.MissingRequestBodyRequiredValueAccessor(),
            p.NonPropertyAttemptedValueIsInvalidAccessor("abc"),
            p.NonPropertyUnknownValueIsInvalidAccessor(),
            p.NonPropertyValueMustBeANumberAccessor(),
            p.UnknownValueIsInvalidAccessor("Kat"),
            p.ValueIsInvalidAccessor("abc"),
            p.ValueMustBeANumberAccessor("Kat"),
            p.ValueMustNotBeNullAccessor("abc")
        };

        foreach (var m in messages)
        {
            Assert.False(string.IsNullOrWhiteSpace(m));
            Assert.DoesNotContain("The ", m);
            Assert.DoesNotContain("must be", m);
            Assert.DoesNotContain("is not valid", m);
        }
        Assert.Equal("'abc' değeri Kat alanı için geçerli değil.", messages[0]);
        Assert.Equal("Kat alanına sayı girin.", messages[9]);
    }

    private class Sample
    {
        [Required] [Display(Name = "Ad")] public string? Name { get; set; }
        [StringLength(50)] [Display(Name = "Şehir")] public string? City { get; set; }
        [StringLength(10, MinimumLength = 2)] [Display(Name = "Kod")] public string? Code { get; set; }
        [Range(1, 100)] [Display(Name = "Oran")] public int Rate { get; set; }
        [EmailAddress] public string? Email { get; set; }
        [Required(ErrorMessage = "Elle yazılmış mesaj.")] public string? Custom { get; set; }
    }

    // Sağlayıcının ürettiği mesajı, kuralın kendi FormatErrorMessage'ıyla (MVC'nin yaptığı gibi) üretir
    private static string MessageFor(string propertyName, string displayName) =>
        MessageFor<ValidationAttribute>(propertyName, displayName, a => a is not RequiredAttribute || propertyName != nameof(Sample.Rate));

    private static string MessageFor<T>(string propertyName, string displayName, Func<T, bool>? pick = null) where T : ValidationAttribute
    {
        var options = Options();
        var provider = options.ModelMetadataDetailsProviders.OfType<IValidationMetadataProvider>().Single();
        var prop = typeof(Sample).GetProperty(propertyName)!;
        var attrs = ModelAttributes.GetAttributesForProperty(typeof(Sample), prop);
        var context = new ValidationMetadataProviderContext(
            ModelMetadataIdentity.ForProperty(prop, prop.PropertyType, typeof(Sample)), attrs);
        foreach (var a in attrs.Attributes.OfType<ValidationAttribute>())
            context.ValidationMetadata.ValidatorMetadata.Add(a);

        provider.CreateValidationMetadata(context);
        return context.ValidationMetadata.ValidatorMetadata.OfType<T>().Single(a => pick == null || pick(a)).FormatErrorMessage(displayName);
    }

    [Fact]
    public void AttributesWithoutMessage_GetTurkishDefaults()
    {
        Assert.Equal("Ad zorunludur.", MessageFor(nameof(Sample.Name), "Ad"));
        Assert.Equal("Şehir en fazla 50 karakter olabilir.", MessageFor(nameof(Sample.City), "Şehir"));
        Assert.Equal("Kod en az 2, en fazla 10 karakter olmalıdır.", MessageFor(nameof(Sample.Code), "Kod"));
        Assert.Equal("Oran 1 ile 100 arasında olmalıdır.", MessageFor(nameof(Sample.Rate), "Oran"));
        Assert.Equal("Geçerli bir e-posta adresi girin.", MessageFor(nameof(Sample.Email), "Email"));
    }

    // Boş olamayan değer tiplerinde (enum, int) MVC'nin gizli "zorunlu" kuralı yerine Türkçesi konur
    [Fact]
    public void ValueTypes_GetTurkishRequired()
    {
        Assert.Equal("Oran zorunludur.", MessageFor<RequiredAttribute>(nameof(Sample.Rate), "Oran"));
    }

    [Fact]
    public void HandWrittenMessages_AreNotChanged()
    {
        Assert.Equal("Elle yazılmış mesaj.", MessageFor(nameof(Sample.Custom), "Custom"));
    }

    // Gerçek MVC kurulumunda (DataAnnotations sağlayıcısından sonra) bir formun kurallarının Türkçe olduğu
    [Fact]
    public void RealMvcSetup_FormMetadata_IsTurkish()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.MvcServiceCollectionExtensions
            .AddControllersWithViews(services, o => TurkishValidationMessages.Apply(o));
        Microsoft.Extensions.DependencyInjection.LoggingServiceCollectionExtensions.AddLogging(services);
        using var sp = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        var provider = (IModelMetadataProvider)sp.GetService(typeof(IModelMetadataProvider))!;

        var listing = provider.GetMetadataForProperty(typeof(EmlakOfisi.WebUI.Models.PropertyFormViewModel), "ListingType");
        var required = listing.ValidatorMetadata.OfType<RequiredAttribute>().Single();
        Assert.Equal("İlan tipi zorunludur.", required.FormatErrorMessage(listing.GetDisplayName()));

        var city = provider.GetMetadataForProperty(typeof(EmlakOfisi.WebUI.Models.PropertyFormViewModel), "City");
        var length = city.ValidatorMetadata.OfType<StringLengthAttribute>().Single();
        Assert.DoesNotContain("maximum length", length.FormatErrorMessage(city.GetDisplayName()));
    }

    // Koruyucu: formlarda kullanılan her doğrulama kuralının ya elle yazılmış mesajı var ya da Türkçe varsayılanı
    // genel ("geçerli değil") mesaja düşmüyor. Yeni bir kural türü eklenirse bu test hatırlatır.
    [Fact]
    public void AllFormAttributes_HaveSpecificTurkishMessage()
    {
        var models = typeof(LoginController).Assembly.GetTypes()
            .Where(t => t.Namespace == "EmlakOfisi.WebUI.Models" && t.IsClass);

        var missing = models
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(p => p.GetCustomAttributes<ValidationAttribute>(true)
                    .Where(a => TurkishValidationMessages.HasDefaultMessage(a))
                    .Where(a => a.GetType() != typeof(DataTypeAttribute))   // DataType(Password) doğrulama yapmaz
                    .Where(a => TurkishValidationMessages.DefaultMessage(a) == "{0} geçerli değil.")
                    .Select(a => $"{t.Name}.{p.Name} ({a.GetType().Name})")))
            .ToList();

        Assert.True(missing.Count == 0, "Türkçe varsayılanı olmayan kural: " + string.Join(", ", missing));
    }
}
