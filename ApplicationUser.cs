using Microsoft.AspNetCore.Identity;

namespace Entity
{
    public class ApplicationUser : IdentityUser
    {
        public string? Ad { get; set; }
        public string? Soyad { get; set; }
        public string? TamAd => $"{Ad} {Soyad}";
        public DateTime? OlusturmaTarihi { get; set; } = DateTime.Now;
        public bool Aktif { get; set; } = true;
        
        
        public virtual Stajyer? Stajyer { get; set; }
        public virtual BirimSorumlusu? BirimSorumlusu { get; set; }
    }
}
