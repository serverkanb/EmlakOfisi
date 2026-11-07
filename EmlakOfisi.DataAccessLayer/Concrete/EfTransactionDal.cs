using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.DataAccessLayer.Concrete
{
    public class EfTransactionDal : ITransactionDal
    {
        private readonly AppDbContext _context;

        public EfTransactionDal(AppDbContext context)
        {
            _context = context;
        }

        private IQueryable<Transaction> Visible(int userId, bool canSeeAll) =>
            _context.Transactions.VisibleTo(userId, canSeeAll);

        private IQueryable<Transaction> Filtered(int userId, bool canSeeAll, TransactionFilter f)
        {
            var query = Visible(userId, canSeeAll);
            query = f.Scope switch
            {
                TransactionScope.Open => query.Where(t => t.Status == TransactionStatus.Offer ||
                                                          t.Status == TransactionStatus.Negotiation ||
                                                          t.Status == TransactionStatus.Agreed),
                TransactionScope.Completed => query.Where(t => t.Status == TransactionStatus.Completed),
                TransactionScope.Cancelled => query.Where(t => t.Status == TransactionStatus.Cancelled),
                _ => query
            };
            if (f.OnlyMine) query = query.Where(t => t.AgentUserId == userId);
            if (f.PropertyId != null) query = query.Where(t => t.PropertyId == f.PropertyId);
            if (f.ContactId != null) query = query.Where(t => t.CustomerContactId == f.ContactId);
            return query;
        }

        // Bağlı kayıtlar görüntüleyenin görünürlük kuralıyla aynı sorguda okunur (N+1 yok).
        // Transaction.Property / CustomerContact navigation'ları YÜKLENMEZ; sadece görünür kopya satıra konur.
        private IQueryable<TransactionRow> ToRows(IQueryable<Transaction> query, int userId, bool canSeeAll)
        {
            var contacts = _context.Contacts.VisibleTo(userId, canSeeAll);
            var properties = _context.Properties.VisibleTo(userId, canSeeAll);

            return query.Select(t => new TransactionRow
            {
                Transaction = t,
                AgentName = t.AgentUser!.Name + " " + t.AgentUser.Surname,
                Contact = contacts.FirstOrDefault(c => c.ContactId == t.CustomerContactId),
                Property = properties.FirstOrDefault(p => p.PropertyId == t.PropertyId)
            });
        }

        public List<TransactionRow> GetVisibleList(int userId, bool canSeeAll, TransactionFilter filter, int skip, int take)
        {
            var query = Filtered(userId, canSeeAll, filter).AsNoTracking()
                .OrderByDescending(t => t.CompletedAt ?? t.CreatedAt).ThenByDescending(t => t.TransactionId)
                .Skip(skip).Take(take);
            return ToRows(query, userId, canSeeAll).ToList();
        }

        public int CountVisible(int userId, bool canSeeAll, TransactionFilter filter)
        {
            return Filtered(userId, canSeeAll, filter).Count();
        }

        public TransactionRow? GetVisibleById(int id, int userId, bool canSeeAll)
        {
            return ToRows(Visible(userId, canSeeAll).AsNoTracking().Where(t => t.TransactionId == id), userId, canSeeAll)
                .FirstOrDefault();
        }

        public Transaction? GetVisibleByIdForUpdate(int id, int userId, bool canSeeAll)
        {
            return Visible(userId, canSeeAll).FirstOrDefault(t => t.TransactionId == id);
        }

        public TransactionSummary GetCompletedSummary(int userId, bool canSeeAll, DateTime from, DateTime to)
        {
            // Danışmanın özeti sadece sorumlusu olduğu işlemlerden (oluşturup başkasına devrettikleri sayılmaz);
            // yönetici tüm ofisi görür.
            var groups = Visible(userId, canSeeAll)
                .Where(t => canSeeAll || t.AgentUserId == userId)
                .Where(t => t.Status == TransactionStatus.Completed && t.CompletedAt >= from && t.CompletedAt < to)
                .GroupBy(t => t.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(),
                    Commission = g.Sum(t => t.CommissionAmount ?? 0),
                    Share = g.Sum(t => t.AgentShareAmount ?? 0),
                    MyShare = g.Sum(t => t.AgentUserId == userId ? (t.AgentShareAmount ?? 0) : 0)
                })
                .ToList();

            return new TransactionSummary
            {
                SaleCount = groups.Where(g => g.Type == ListingType.Sale).Sum(g => g.Count),
                RentCount = groups.Where(g => g.Type == ListingType.Rent).Sum(g => g.Count),
                Commission = groups.Sum(g => g.Commission),
                AgentShare = groups.Sum(g => g.Share),
                MyAgentShare = groups.Sum(g => g.MyShare)
            };
        }

        public decimal? GetAgentShareRate(int agentUserId)
        {
            return _context.Users.Where(u => u.Id == agentUserId).Select(u => u.CommissionSharePercent).FirstOrDefault();
        }

        public void Create(Transaction transaction)
        {
            _context.Transactions.Add(transaction);
            _context.SaveChanges();
        }

        public void Update(Transaction transaction)
        {
            if (_context.Entry(transaction).State == EntityState.Detached)
                throw new InvalidOperationException("Güncellenecek işlem önce GetVisibleByIdForUpdate ile okunmalıdır.");
            _context.SaveChanges();
        }
    }
}
