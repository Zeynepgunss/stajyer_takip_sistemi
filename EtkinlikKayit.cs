namespace Entity
{
    public class EtkinlikKayit
    {
        public int Id { get; set; }
        public int EtkinlikId { get; set; }
        public string? UserId { get; set; }
        public string? KatilimciAdi { get; set; }
        public string? KatilimciEmail { get; set; }
        public DateTime KayitTarihi { get; set; } = DateTime.Now;

        public virtual Etkinlik? Etkinlik { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}
