using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace EmlakOfisi.WebUI.Extensions
{
    // Formlardaki İngilizce varsayılan mesajların Türkçesi (tek yer). Alan adı formdaki Türkçe etiketten gelir
    // ([Display(Name = "Kat")] -> "Kat"). Elle yazılmış ErrorMessage'lara dokunulmaz.
    public static class TurkishValidationMessages
    {
        public static void Apply(MvcOptions options)
        {
            // 1) Değer türe uymayınca (sayı alanına harf, "5.000" vb.). Telefonda anında çıkan
            //    "must be a number" uyarısı da buradan (ValueMustBeANumber) üretilir.
            var p = options.ModelBindingMessageProvider;
            p.SetAttemptedValueIsInvalidAccessor((value, field) => $"'{value}' değeri {field} alanı için geçerli değil.");
            p.SetMissingBindRequiredValueAccessor(field => $"{field} alanı için değer girilmedi.");
            p.SetMissingKeyOrValueAccessor(() => "Değer girilmedi.");
            p.SetMissingRequestBodyRequiredValueAccessor(() => "Gönderilen form boş.");
            p.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"'{value}' değeri geçerli değil.");
            p.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Girilen değer geçerli değil.");
            p.SetNonPropertyValueMustBeANumberAccessor(() => "Bu alana sayı girin.");
            p.SetUnknownValueIsInvalidAccessor(field => $"{field} alanına girilen değer geçerli değil.");
            p.SetValueIsInvalidAccessor(value => $"'{value}' değeri geçerli değil.");
            p.SetValueMustBeANumberAccessor(field => $"{field} alanına sayı girin.");
            p.SetValueMustNotBeNullAccessor(_ => "Bu alan boş bırakılamaz.");

            // 2) Mesajı yazılmamış doğrulama kurallarına Türkçe varsayılan (DataAnnotations sağlayıcısından SONRA çalışır)
            options.ModelMetadataDetailsProviders.Add(new DefaultMessageProvider());
        }

        // Mesajı olmayan kural için Türkçe şablon ({0} alan adı; {1}, {2} kuralın değerleri). Bilinmeyen kural -> genel mesaj.
        public static string DefaultMessage(ValidationAttribute attribute) => attribute switch
        {
            RequiredAttribute => "{0} zorunludur.",
            StringLengthAttribute s when s.MinimumLength > 0 => "{0} en az {2}, en fazla {1} karakter olmalıdır.",
            StringLengthAttribute => "{0} en fazla {1} karakter olabilir.",
            MaxLengthAttribute => "{0} en fazla {1} karakter olabilir.",
            MinLengthAttribute => "{0} en az {1} karakter olmalıdır.",
            RangeAttribute => "{0} {1} ile {2} arasında olmalıdır.",
            EmailAddressAttribute => "Geçerli bir e-posta adresi girin.",
            PhoneAttribute => "Geçerli bir telefon numarası girin.",
            UrlAttribute => "Geçerli bir web adresi girin.",
            CompareAttribute => "{0} ile {1} aynı olmalıdır.",
            RegularExpressionAttribute => "{0} geçerli biçimde değil.",
            _ => "{0} geçerli değil."
        };

        // Mesaj elle yazılmamış mı? E-posta, telefon, web adresi gibi kurallar İngilizce varsayılanı ErrorMessage
        // olarak döndürür; aynı türün yeni örneğindeki mesajla aynıysa elle yazılmamış sayılır.
        public static bool HasDefaultMessage(ValidationAttribute attribute)
        {
            if (attribute.ErrorMessageResourceType != null) return false;
            // Sadece .NET'in kendi kuralları: projede ileride kendi mesajını base("...") ile veren bir kural yazılırsa ezilmesin
            if (attribute.GetType().Assembly != typeof(RequiredAttribute).Assembly) return false;
            if (string.IsNullOrEmpty(attribute.ErrorMessage)) return true;
            var type = attribute.GetType();
            return type.GetConstructor(Type.EmptyTypes) != null &&
                   ((ValidationAttribute)Activator.CreateInstance(type)!).ErrorMessage == attribute.ErrorMessage;
        }

        private sealed class DefaultMessageProvider : IValidationMetadataProvider
        {
            public void CreateValidationMetadata(ValidationMetadataProviderContext context)
            {
                // Boş olamayan değer tiplerine (enum, int, bool...) MVC sonradan İngilizce mesajlı gizli bir "zorunlu"
                // kuralı ekler (telefonda anında çıkan uyarı). Önceden Türkçe mesajlı kural konursa o kullanılır.
                var type = context.Key.ModelType;
                if (context.Key.MetadataKind == ModelMetadataKind.Property && type.IsValueType &&
                    Nullable.GetUnderlyingType(type) == null &&
                    !context.ValidationMetadata.ValidatorMetadata.OfType<RequiredAttribute>().Any())
                {
                    context.ValidationMetadata.ValidatorMetadata.Add(new RequiredAttribute { ErrorMessage = "{0} zorunludur." });
                }

                foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
                {
                    // Elle yazılmış mesaj ya da kaynak dosyası varsa dokunma
                    if (!HasDefaultMessage(attribute))
                        continue;
                    attribute.ErrorMessage = DefaultMessage(attribute);
                }
            }
        }
    }
}
