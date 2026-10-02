namespace Entity
{
    public class GunlukGorev
    {
        public int Id { get; set; }
        public int StajyerId { get; set; }
        public DateTime Tarih { get; set; } = DateTime.Now;
        public string? YapilanIs { get; set; }
        public string? KullanilanTeknoloji { get; set; } 
        public decimal CalismaSaati { get; set; }
        public string? DosyaYolu { get; set; } 
        public string? DosyaAdi { get; set; }
        public bool Onaylandi { get; set; } = false;
        public DateTime? OnayTarihi { get; set; }
        public string? OnaylayanUserId { get; set; }
        public string? Yorum { get; set; }
        public int? Puan { get; set; } 
        public bool GecTeslim { get; set; } = false;
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        
        
        public virtual Stajyer? Stajyer { get; set; }
        public virtual ApplicationUser? OnaylayanUser { get; set; }
    }
}
