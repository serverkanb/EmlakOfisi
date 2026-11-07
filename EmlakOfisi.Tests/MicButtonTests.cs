using System.Text.RegularExpressions;

namespace EmlakOfisi.Tests;

// Görev 16b: bas-konuş mikrofon düğmesi. Sadece serbest metin notlarında (textarea) kullanılır;
// telefon, fiyat, tarih gibi yapılandırılmış alanlara eklenmez.
public class MicButtonTests
{
    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "EmlakOfisi.WebUI", "Views")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "EmlakOfisi.WebUI");
    }

    private static IEnumerable<(string File, string Tag)> MicTags() =>
        Directory.GetFiles(Path.Combine(WebRoot(), "Views"), "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"<(\w+)[^>]*\sdata-mic[\s>/=]", RegexOptions.Singleline)
                .Select(m => (Path.GetFileName(f), m.Groups[1].Value)));

    [Fact]
    public void DataMic_IsOnlyOnTextareas()
    {
        var wrong = MicTags().Where(t => !t.Tag.Equals("textarea", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(wrong.Count == 0, "data-mic textarea dışında: " + string.Join(", ", wrong));
    }

    [Fact]
    public void DataMic_IsOnExpectedFreeTextFields()
    {
        var files = MicTags().Select(t => t.File).OrderBy(f => f).ToList();
        Assert.Equal(new[] { "_ActivityHistory.cshtml", "_AppointmentForm.cshtml", "_PropertyForm.cshtml", "_RequestForm.cshtml" }, files);

        // Portföy formunda sadece açıklama (adres alanı yapılandırılmış sayılır)
        var property = File.ReadAllText(Path.Combine(WebRoot(), "Views", "Property", "_PropertyForm.cshtml"));
        Assert.Matches(@"<textarea asp-for=""Description""[^>]*data-mic", property);
        Assert.DoesNotMatch(@"<textarea asp-for=""Address""[^>]*data-mic", property);
    }

    [Fact]
    public void MicScript_IsLoadedInLayout_AndOnlyTargetsTextareas()
    {
        var layout = File.ReadAllText(Path.Combine(WebRoot(), "Views", "Shared", "_Layout.cshtml"));
        Assert.Contains("~/js/mic.js", layout);

        var js = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "js", "mic.js"));
        // Davranış elle (tarayıcıda) denenir; burada sadece temel sözleşme: hedef textarea, dil Türkçe, güvenli bağlantı şartı
        Assert.Contains("textarea[data-mic]", js);
        Assert.Contains("tr-TR", js);
        Assert.Contains("isSecureContext", js);
    }
}
