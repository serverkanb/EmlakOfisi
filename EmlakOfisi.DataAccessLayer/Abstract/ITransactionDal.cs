using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Satış/kiralama işlemleri. Görünürlük kuralı (VisibilityRules) sorguların İÇİNDE uygulanır.
    // Generic yapıdan TÜREMEZ: filtresiz liste/okuma ve gerçek silme yoktur (iptal = durum).
    public interface ITransactionDal
    {
        // Bağlı portföy/müşteri aynı sorguda, görüntüleyenin görünürlüğüyle süzülerek gelir
        List<TransactionRow> GetVisibleList(int userId, bool canSeeAll, TransactionFilter filter, int skip, int take);

        int CountVisible(int userId, bool canSeeAll, TransactionFilter filter);

        TransactionRow? GetVisibleById(int id, int userId, bool canSeeAll);

        Transaction? GetVisibleByIdForUpdate(int id, int userId, bool canSeeAll);

        // [from, to) aralığında tamamlanan, görünür işlemlerin özeti (tek sorgu)
        TransactionSummary GetCompletedSummary(int userId, bool canSeeAll, DateTime from, DateTime to);

        void Create(Transaction transaction);

        void Update(Transaction transaction);
    }
}
