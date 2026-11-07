namespace EmlakOfisi.BusinessLayer.Helpers
{
    // Kullanıcının girdiği dış linkler (ör. sahibinden.com ilan linki) için tek kontrol.
    // Sadece tam bir http/https adresi kabul edilir; "javascript:", "data:", göreli adres vb. reddedilir.
    public static class UrlValidator
    {
        public const int MaxLength = 500;

        public static bool IsSafeHttpUrl(string? url) =>
            !string.IsNullOrWhiteSpace(url) && url.Length <= MaxLength &&
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
            !string.IsNullOrEmpty(uri.Host);
    }
}
