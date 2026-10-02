using Entity;

namespace BusinessLayer
{
    public interface IPerformansHesaplamaService
    {
        Task<PerformansSkoru> HesaplaPerformansSkoruAsync(int stajyerId, DateTime baslangicTarihi, DateTime bitisTarihi);
        Task<List<PerformansSkoru>> HesaplaHaftalikPerformansAsync(int stajyerId);
    }
}
