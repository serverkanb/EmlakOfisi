using System.Text.Json;
using System.Text.RegularExpressions;

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

    [Fact]
    public void Manifest_IsValid_AndIconsExist()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(WwwRoot(), "manifest.webmanifest")));
        var root = doc.RootElement;

        Assert.Equal("/", root.GetProperty("start_url").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("name").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("short_name").GetString()));

        var icons = root.GetProperty("icons").EnumerateArray().ToList();
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "192x192");
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "512x512");
        foreach (var icon in icons)
            Assert.True(File.Exists(FileFor(icon.GetProperty("src").GetString()!)), "İkon dosyası yok: " + icon.GetProperty("src"));
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
