using System.ComponentModel.DataAnnotations.Schema;

namespace Entity
{
    [Table("vw_BirimSorumlulariDetay")]
    public class BirimSorumlusuDetayView
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        
        
        public string? KullaniciAdi { get; set; }
        public string? KullaniciSoyadi { get; set; }
        public string? SorumluAdi { get; set; }
        public string? KullaniciEmail { get; set; }
        public string? KullaniciUserName { get; set; }
        
        
        public int BirimId { get; set; }
        public string? BirimAdi { get; set; }
        public int? BirimKontenjan { get; set; }
        public int? BirimMevcutStajyerSayisi { get; set; }
        
        
        public DateTime AtamaTarihi { get; set; }
        public bool Aktif { get; set; }
    }
}
