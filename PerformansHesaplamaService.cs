using DataAccessLayer;
using Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer
{
    public class PerformansHesaplamaService : IPerformansHesaplamaService
    {
        private readonly ApplicationDbContext _context;

        public PerformansHesaplamaService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PerformansSkoru> HesaplaPerformansSkoruAsync(int stajyerId, DateTime baslangicTarihi, DateTime bitisTarihi)
        {
            var stajyer = await _context.Stajyerler
                .Include(s => s.GunlukGorevler)
                .FirstOrDefaultAsync(s => s.Id == stajyerId);

            if (stajyer == null)
                throw new ArgumentException("Stajyer bulunamadı");

            var gorevler = stajyer.GunlukGorevler
                .Where(g => g.Tarih >= baslangicTarihi && g.Tarih <= bitisTarihi)
                .ToList();

            // 1. Günlük Giriş Düzeni (0-100)
            var toplamGun = (bitisTarihi - baslangicTarihi).Days + 1;
            var girisYapilanGun = gorevler.Select(g => g.Tarih.Date).Distinct().Count();
            var gunlukGirisDuzeni = toplamGun > 0 ? (decimal)(girisYapilanGun * 100.0 / toplamGun) : 0;

            // 2. Görev Tamamlama Oranı
            var tamamlananGorev = gorevler.Count(g => g.Onaylandi);
            var toplamGorev = gorevler.Count;
            var gorevTamamlamaOrani = toplamGorev > 0 ? (decimal)(tamamlananGorev * 100.0 / toplamGorev) : 0;

            // 3. Sorumlu Değerlendirmesi (Ortalama Puan)
            var puanliGorevler = gorevler.Where(g => g.Puan.HasValue && g.Puan.Value > 0).ToList();
            var sorumluDegerlendirmesi = puanliGorevler.Any() 
                ? (decimal)puanliGorevler.Average(g => g.Puan!.Value) 
                : 0;

            // 4. Geç Teslim Oranı (Tersine çevrilmiş - düşük geç teslim = yüksek skor)
            var gecTeslimSayisi = gorevler.Count(g => g.GecTeslim);
            var gecTeslimOrani = toplamGorev > 0 
                ? 100 - (decimal)(gecTeslimSayisi * 100.0 / toplamGorev)
                : 100;

            // Toplam Skor (Ağırlıklı ortalama)
            var toplamSkor = (gunlukGirisDuzeni * 0.2m) +
                            (gorevTamamlamaOrani * 0.3m) +
                            (sorumluDegerlendirmesi * 0.3m) +
                            (gecTeslimOrani * 0.2m);

            var performansSkoru = new PerformansSkoru
            {
                StajyerId = stajyerId,
                BaslangicTarihi = baslangicTarihi,
                BitisTarihi = bitisTarihi,
                GunlukGirisDuzeni = Math.Round(gunlukGirisDuzeni, 2),
                GorevTamamlamaOrani = Math.Round(gorevTamamlamaOrani, 2),
                SorumluDegerlendirmesi = Math.Round(sorumluDegerlendirmesi, 2),
                GecTeslimOrani = Math.Round(gecTeslimOrani, 2),
                ToplamSkor = Math.Round(toplamSkor, 2)
            };

            _context.PerformansSkorlari.Add(performansSkoru);
            await _context.SaveChangesAsync();

            return performansSkoru;
        }

        public async Task<List<PerformansSkoru>> HesaplaHaftalikPerformansAsync(int stajyerId)
        {
            var stajyer = await _context.Stajyerler.FindAsync(stajyerId);
            if (stajyer == null)
                throw new ArgumentException("Stajyer bulunamadı");

            var haftalikPerformanslar = new List<PerformansSkoru>();
            var baslangic = stajyer.BaslangicTarihi;
            var bitis = DateTime.Now < stajyer.BitisTarihi ? DateTime.Now : stajyer.BitisTarihi;

            var haftaBaslangic = baslangic;
            while (haftaBaslangic < bitis)
            {
                var haftaBitis = haftaBaslangic.AddDays(6);
                if (haftaBitis > bitis)
                    haftaBitis = bitis;

                var performans = await HesaplaPerformansSkoruAsync(stajyerId, haftaBaslangic, haftaBitis);
                haftalikPerformanslar.Add(performans);

                haftaBaslangic = haftaBitis.AddDays(1);
            }

            return haftalikPerformanslar;
        }
    }
}
