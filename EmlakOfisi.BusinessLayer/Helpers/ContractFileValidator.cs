namespace EmlakOfisi.BusinessLayer.Helpers
{
    // Kontrat dosyası (görev 22): PDF, JPG ya da PNG. Tür, içeriğin ilk baytlarından anlaşılır;
    // dosya adı uzantısına ve tarayıcının söylediği tipe güvenilmez (ör. "kontrat.pdf" adlı bir HTML dosyası).
    public static class ContractFileValidator
    {
        public const int MaxBytes = 10 * 1024 * 1024; // 10 MB (telefonla taranmış birkaç sayfalık PDF yeter)

        public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> header)
        {
            // "%PDF-"
            if (header.Length >= 5 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D)
                return ("application/pdf", ".pdf");

            // JPEG ve PNG resim doğrulayıcısından (WebP kontrat için kabul edilmez)
            var image = ImageFileValidator.Detect(header);
            return image is { ContentType: "image/jpeg" or "image/png" } ? image : null;
        }
    }
}
