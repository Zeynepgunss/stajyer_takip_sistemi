namespace Entity
{
    public class StajyerYetenek
    {
        public int Id { get; set; }
        public int StajyerId { get; set; }
        public int YetenekId { get; set; }
        public int Seviye { get; set; } 
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellemeTarihi { get; set; }
        
        
        public virtual Stajyer? Stajyer { get; set; }
        public virtual Yetenek? Yetenek { get; set; }
    }
}
