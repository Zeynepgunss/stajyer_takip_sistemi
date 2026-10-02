namespace Entity
{
    public class BirimSorumlusu
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int BirimId { get; set; }
        public DateTime AtamaTarihi { get; set; } = DateTime.Now;
        public bool Aktif { get; set; } = true;
        
        
        public virtual ApplicationUser? User { get; set; }
        public virtual Birim? Birim { get; set; }
    }
}
