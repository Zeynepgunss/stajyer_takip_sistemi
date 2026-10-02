using System.ComponentModel.DataAnnotations.Schema;

namespace Entity
{
    [Table("vw_StajyerlerDetay")]
    public class StajyerDetayView
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        
        
        public string? KullaniciAdi { get; set; }
        public string? KullaniciSoyadi { get; set; }
        public string? StajyerAdi { get; set; }
        public string? KullaniciEmail { get; set; }
        public string? KullaniciUserName { get; set; }
        
        
        public int BirimId { get; set; }
        public string? BirimAdi { get; set; }
        public string? Okul { get; set; }
        public string? Bolum { get; set; }
        public string? Sinif { get; set; }
        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }
        public int ToplamGun { get; set; }
        public int TamamlananGun { get; set; }
        public decimal DevamOrani { get; set; }
        public bool Aktif { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
    }
}
