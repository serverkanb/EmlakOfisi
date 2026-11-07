namespace EmlakOfisi.BusinessLayer.Helpers
{
    // Komisyon tutarı: anlaşılan fiyat x oran / 100, 2 haneye yuvarlanır (yarım kuruş yukarı).
    // Oran ya da fiyat yoksa null (tutar elle girilir).
    public static class CommissionCalculator
    {
        public static decimal? Calculate(decimal? agreedPrice, decimal? rate) =>
            agreedPrice == null || rate == null
                ? null
                : Math.Round(agreedPrice.Value * rate.Value / 100m, 2, MidpointRounding.AwayFromZero);
    }
}
