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

        // [from, to) aralığında tamamlanan işlemlerin özeti (tek sorgu). Danışman: sorumlusu olduğu işlemler; yönetici: hepsi
        TransactionSummary GetCompletedSummary(int userId, bool canSeeAll, DateTime from, DateTime to);

        // Danışmanın güncel prim oranı (Admin'in yazdığı); yoksa null
        decimal? GetAgentShareRate(int agentUserId);

        void Create(Transaction transaction);

        void Update(Transaction transaction);
    }
}
