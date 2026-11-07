using System.Text.Json;
using System.Text.RegularExpressions;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

// Görev 16: PWA dosyaları. En önemli kural: service worker sadece statik dosyaları önbelleğe alır;
// sayfalar, fotoğraflar ve diğer uçlar (müşteri/portföy verisi) telefonda saklanmaz.
public class PwaTests
{
    private static string WwwRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "EmlakOfisi.WebUI", "wwwroot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "EmlakOfisi.WebUI", "wwwroot");
    }

    private static string ServiceWorker() => File.ReadAllText(Path.Combine(WwwRoot(), "sw.js"));

    private static string FileFor(string urlPath) =>
        Path.Combine(WwwRoot(), urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    // appsettings.json "Office" bölümü (uygulamanın açılışta okuduğu ayar)
    private static OfficeSettings AppSettingsOffice()
    {
        var path = Path.Combine(Directory.GetParent(WwwRoot())!.FullName, "appsettings.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var office = JsonSerializer.Deserialize<OfficeSettings>(doc.RootElement.GetProperty(OfficeSettings.SectionName).GetRawText());
        return office!.Normalize();
    }

    private static JsonElement ManifestFor(OfficeSettings office)
    {
        var controller = new ManifestController(office)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result = Assert.IsType<JsonResult>(controller.Index());
        Assert.StartsWith("application/manifest+json", result.ContentType);
        return JsonSerializer.SerializeToElement(result.Value);
    }

    [Fact]
    public void Manifest_FromAppSettings_IsValid_AndIconsExist()
    {
        var office = AppSettingsOffice();
        var root = ManifestFor(office);

        Assert.Equal("/", root.GetProperty("start_url").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal(office.Name, root.GetProperty("name").GetString());
        Assert.Equal(office.ShortName, root.GetProperty("short_name").GetString());

        var icons = root.GetProperty("icons").EnumerateArray().ToList();
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "192x192");
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "512x512");
        foreach (var icon in icons)
            Assert.True(File.Exists(FileFor(icon.GetProperty("src").GetString()!)), "İkon dosyası yok: " + icon.GetProperty("src"));

        // Ayardaki logo dosyaları da gerçekten var
        foreach (var p in new[] { office.LogoPath, office.LogoWhitePath, office.Icon64Path })
            Assert.True(p != null && File.Exists(FileFor(p)), "Logo/ikon dosyası yok: " + p);
    }

    [Fact]
    public void Manifest_UsesOfficeSettings()
    {
        var root = ManifestFor(new OfficeSettings { Name = "Deneme Emlak", ShortName = "Deneme", Icon192Path = "/x/192.png" }.Normalize());

        Assert.Equal("Deneme Emlak", root.GetProperty("name").GetString());
        Assert.Equal("Deneme", root.GetProperty("short_name").GetString());
        var icon = Assert.Single(root.GetProperty("icons").EnumerateArray());
        Assert.Equal("/x/192.png", icon.GetProperty("src").GetString());
    }

    [Fact]
    public void ManifestController_IsAnonymous_AndHasNoOtherActions()
    {
        Assert.NotNull(typeof(ManifestController).GetCustomAttributes(typeof(AllowAnonymousAttribute), false).SingleOrDefault());
        var actions = typeof(ManifestController).GetMethods(System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { nameof(ManifestController.Index) }, actions.Select(a => a.Name));
    }

    [Fact]
    public void OfficeSettings_Normalize_FallsBackSafely()
    {
        var empty = new OfficeSettings { Name = "  ", ShortName = "" }.Normalize();
        Assert.Equal(OfficeSettings.DefaultName, empty.Name);
        Assert.Equal(OfficeSettings.DefaultName, empty.ShortName);
        Assert.Null(empty.LogoPath);

        var named = new OfficeSettings { Name = " Kule Emlak ", ShortName = null! }.Normalize();
        Assert.Equal("Kule Emlak", named.Name);
        Assert.Equal("Kule Emlak", named.ShortName);   // kısa ad boşsa ad
    }

    [Theory]
    [InlineData("/images/brand/a.png", "/images/brand/a.png")]
    [InlineData("~/images/a.png", "/images/a.png")]
    [InlineData("https://kotu.example/logo.png", null)]   // dış adres
    [InlineData("//kotu.example/logo.png", null)]          // protokolsüz dış adres
    [InlineData("javascript:alert(1)", null)]
    [InlineData("images/a.png", null)]                     // göreli yol
    [InlineData("/images\\a.png", null)]
    [InlineData("", null)]
    [InlineData("/\t/kotu.example/logo.png", null)]          // tarayıcı sekmeyi siler -> "//host"
    [InlineData("/\n/kotu.example/logo.png", null)]
    [InlineData("/images/brand/a b.png", null)]
    public void OfficeSettings_LocalPath_OnlySiteLocal(string input, string? expected)
    {
        Assert.Equal(expected, OfficeSettings.LocalPath(input));
    }

    // Ofis adı koda yeniden sabit yazılmasın (CLAUDE.md): görünümlerde, statik dosyalarda ve controller'larda geçmez
    [Fact]
    public void OfficeName_IsNotHardcoded()
    {
        var webRoot = Directory.GetParent(WwwRoot())!.FullName;
        var files = Directory.GetFiles(Path.Combine(webRoot, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(webRoot, "Controllers"), "*.cs"))
            .Concat(Directory.GetFiles(Path.Combine(webRoot, "Helpers"), "*.cs"))
            .Append(Path.Combine(webRoot, "Program.cs"))
            .Concat(Directory.GetFiles(WwwRoot(), "*.html"))
            .Concat(Directory.GetFiles(WwwRoot(), "*.js"))
            .Concat(Directory.GetFiles(Path.Combine(WwwRoot(), "js"), "*.js"));

        var offenders = files.Where(f => Regex.IsMatch(File.ReadAllText(f), "alt[ıi]nkule", RegexOptions.IgnoreCase))
            .Select(Path.GetFileName).ToList();
        Assert.True(offenders.Count == 0, "Ofis adı/logo dosyası koda sabit yazılmış: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ServiceWorker_CachesOnlyStaticFolders()
    {
        var sw = ServiceWorker();
        var prefixes = Regex.Match(sw, @"const STATIC_PREFIXES = \[(.*?)\];").Groups[1].Value;

        Assert.Equal(new[] { "/lib/", "/css/", "/js/", "/images/" },
            Regex.Matches(prefixes, @"'([^']*)'").Select(m => m.Groups[1].Value));
    }

    [Fact]
    public void ServiceWorker_PrecachesOnlyStaticFilesAndOfflinePage_AllExist()
    {
        var sw = ServiceWorker();
        var block = Regex.Match(sw, @"const PRECACHE_URLS = \[(.*?)\];", RegexOptions.Singleline).Groups[1].Value;
        var urls = Regex.Matches(block, @"'([^']*)'").Select(m => m.Groups[1].Value).ToList();

        Assert.Contains("OFFLINE_URL", block);
        Assert.NotEmpty(urls);
        foreach (var url in urls)
        {
            Assert.Matches(@"^/(lib|css|js|images)/", url);
            Assert.True(File.Exists(FileFor(url)), "Önbelleğe alınacak dosya yok: " + url);
        }
        Assert.True(File.Exists(Path.Combine(WwwRoot(), "offline.html")));
    }

    [Fact]
    public void ServiceWorker_PagesAndPostsAreNeverCached()
    {
        var sw = ServiceWorker();

        // POST ve başka sitelere giden istekler hiç ele alınmaz
        Assert.Contains("if (request.method !== 'GET') return;", sw);
        Assert.Contains("if (url.origin !== self.location.origin) return;", sw);

        // Sayfa açılışı sadece ağdan; bu dalda önbelleğe yazma yok
        var navigate = Regex.Match(sw, @"if \(request\.mode === 'navigate'\) \{(.*?)return;\s*\}", RegexOptions.Singleline).Groups[1].Value;
        Assert.Contains("fetch(request)", navigate);
        Assert.DoesNotContain("cache.put", navigate);

        // Önbelleğe yazma tek yerde ve statik dal içinde
        Assert.Single(Regex.Matches(sw, @"cache\.put\("));
        var putIndex = sw.IndexOf("cache.put(", StringComparison.Ordinal);
        var staticIndex = sw.IndexOf("if (isStatic(url))", StringComparison.Ordinal);
        Assert.True(staticIndex > 0 && putIndex > staticIndex);
    }

    [Fact]
    public void OfflinePage_HasNoDynamicContent()
    {
        var html = File.ReadAllText(Path.Combine(WwwRoot(), "offline.html"));
        Assert.DoesNotContain("@", html.Replace("@media", ""));   // Razor değil, statik
        Assert.DoesNotContain("<form", html);
    }
}
