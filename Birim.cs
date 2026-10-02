namespace Entity
{
    public class Birim
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public int Kontenjan { get; set; }
        public int MevcutStajyerSayisi { get; set; }
        public bool Aktif { get; set; } = true;
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        
        
        public virtual ICollection<Stajyer> Stajyerler { get; set; } = new List<Stajyer>();
        public virtual ICollection<BirimSorumlusu> Sorumlular { get; set; } = new List<BirimSorumlusu>();
    }
}
