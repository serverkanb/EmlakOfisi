namespace EmlakOfisi.BusinessLayer.Abstract
{
    // InstrumentTrade'deki IGenericService ile aynı (T önekli metotlar).
    // IContactService ve IPropertyService bundan türemeyecek; görünürlük filtreli kendi metotlarını sunacak.
    public interface IGenericService<T> where T : class
    {
        List<T> TGetList();

        T? TGetById(int id);

        void TCreate(T entity);

        void TUpdate(T entity);

        void TDelete(int id);
    }
}
