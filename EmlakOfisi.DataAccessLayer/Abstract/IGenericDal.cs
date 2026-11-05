namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // CRUD işlemleri (InstrumentTrade'deki IGenericDal ile aynı).
    // Not: Contact ve Property için controller'lar bu filtresiz metotları doğrudan kullanmaz;
    // onların servisleri görünürlük filtreli metotlar sunar (görev 7 ve 11).
    public interface IGenericDal<T> where T : class
    {
        List<T> GetList();

        T? GetById(int id);

        void Create(T entity);

        void Update(T entity);

        void Delete(int id);
    }
}
