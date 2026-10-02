namespace Entity
{
    
    public class StajyerProje
    {
        public int Id { get; set; }
        public int StajyerId { get; set; }
        public string ProjeAdi { get; set; } = string.Empty;
        public string? GitHubLink { get; set; }
        
        public string? KullanilanDil { get; set; }
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        public virtual Stajyer? Stajyer { get; set; }
    }
}
