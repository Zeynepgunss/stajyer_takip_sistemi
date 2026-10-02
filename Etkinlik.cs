namespace Entity
{
    public class Etkinlik
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        /// <summary>Kısa açıklama (max 512 karakter).</summary>
        public string? KisaAciklama { get; set; }
        /// <summary>Detaylı açıklama (HTML olabilir).</summary>
        public string? Aciklama { get; set; }
        public DateTime Tarih { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public string? Saat { get; set; }
        public string? Yer { get; set; }

        public string? GorselDosyaAdi { get; set; }
        public int? Kontenjan { get; set; }
        public bool Aktif { get; set; } = true;
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        public virtual ICollection<EtkinlikKayit> Kayitlar { get; set; } = new List<EtkinlikKayit>();
    }
}
