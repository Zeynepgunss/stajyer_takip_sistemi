namespace Entity
{
    public class PerformansSkoru
    {
        public int Id { get; set; }
        public int StajyerId { get; set; }
        public DateTime HesaplamaTarihi { get; set; } = DateTime.Now;
        public DateTime BaslangicTarihi { get; set; } 
        public DateTime BitisTarihi { get; set; }
        
        
        public decimal GunlukGirisDuzeni { get; set; } 
        public decimal GorevTamamlamaOrani { get; set; } 
        public decimal SorumluDegerlendirmesi { get; set; } 
        public decimal GecTeslimOrani { get; set; } 
        
        
        public decimal ToplamSkor { get; set; }
        
        
        public decimal? SorumluFinalPuanı { get; set; }
        
        
        public virtual Stajyer? Stajyer { get; set; }
    }
}
