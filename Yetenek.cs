namespace Entity
{
    public class Yetenek
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty; 
        public string? Kategori { get; set; } 
        public bool Aktif { get; set; } = true;
        
        
        public virtual ICollection<StajyerYetenek> StajyerYetenekler { get; set; } = new List<StajyerYetenek>();
    }
}
