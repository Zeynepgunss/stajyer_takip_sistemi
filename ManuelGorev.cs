namespace Entity
{
    /// <summary>
    /// IT Yöneticisi tarafından kullanıcıya atanan manuel görev (Rol Ata ekranındaki metin).
    /// İlgili kullanıcının anasayfasında listelenir.
    /// </summary>
    public class ManuelGorev
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Aciklama { get; set; } = string.Empty;
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public string? AtayanUserId { get; set; }

        public virtual ApplicationUser? User { get; set; }
    }
}