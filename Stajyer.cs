namespace Entity
{
    public class Stajyer
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int BirimId { get; set; }
        public string? Okul { get; set; }
        public string? Bolum { get; set; }
        public string? Sinif { get; set; }
        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }
        public int ToplamGun { get; set; }
        public int TamamlananGun { get; set; }
        public decimal DevamOrani { get; set; }
        public bool Aktif { get; set; } = true;
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        
        
        public virtual ApplicationUser? User { get; set; }
        public virtual Birim? Birim { get; set; }
        public virtual ICollection<GunlukGorev> GunlukGorevler { get; set; } = new List<GunlukGorev>();
        public virtual ICollection<StajyerYetenek> Yetenekler { get; set; } = new List<StajyerYetenek>();
        public virtual ICollection<PerformansSkoru> PerformansSkorlari { get; set; } = new List<PerformansSkoru>();
        public virtual ICollection<StajyerProje> Projeler { get; set; } = new List<StajyerProje>();
    }
}
