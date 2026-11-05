using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Context;

namespace EmlakOfisi.DataAccessLayer.Repositories
{
    public class GenericRepository<T> : IGenericDal<T> where T : class
    {
        private readonly AppDbContext _appDbContext;

        public GenericRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public void Create(T entity)
        {
            _appDbContext.Add(entity);
            _appDbContext.SaveChanges();
        }

        public void Delete(int id)
        {
            var value = _appDbContext.Set<T>().Find(id);
            if (value == null)
                return;

            _appDbContext.Set<T>().Remove(value);
            _appDbContext.SaveChanges();
        }

        public T? GetById(int id)
        {
            return _appDbContext.Set<T>().Find(id);
        }

        public List<T> GetList()
        {
            return _appDbContext.Set<T>().ToList();
        }

        public void Update(T entity)
        {
            _appDbContext.Update(entity);
            _appDbContext.SaveChanges();
        }
    }
}
