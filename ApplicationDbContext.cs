using Entity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Birim> Birimler { get; set; }
        public DbSet<Stajyer> Stajyerler { get; set; }
        public DbSet<BirimSorumlusu> BirimSorumlulari { get; set; }
        public DbSet<GunlukGorev> GunlukGorevler { get; set; }
        public DbSet<Yetenek> Yetenekler { get; set; }
        public DbSet<StajyerYetenek> StajyerYetenekler { get; set; }
        public DbSet<PerformansSkoru> PerformansSkorlari { get; set; }
        public DbSet<StajyerProje> StajyerProjeler { get; set; }
        public DbSet<Etkinlik> Etkinlikler { get; set; }
        public DbSet<EtkinlikKayit> EtkinlikKayitlari { get; set; }
        public DbSet<ManuelGorev> ManuelGorevler { get; set; }

        
        public DbSet<StajyerDetayView> StajyerlerDetay { get; set; }
        public DbSet<BirimSorumlusuDetayView> BirimSorumlulariDetay { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            
            builder.Entity<Stajyer>()
                .HasOne(s => s.Birim)
                .WithMany(b => b.Stajyerler)
                .HasForeignKey(s => s.BirimId)
                .OnDelete(DeleteBehavior.Restrict);

            
            builder.Entity<BirimSorumlusu>()
                .HasOne(bs => bs.Birim)
                .WithMany(b => b.Sorumlular)
                .HasForeignKey(bs => bs.BirimId)
                .OnDelete(DeleteBehavior.Restrict);

            
            builder.Entity<Stajyer>()
                .HasOne(s => s.User)
                .WithOne(u => u.Stajyer)
                .HasForeignKey<Stajyer>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            
            builder.Entity<BirimSorumlusu>()
                .HasOne(bs => bs.User)
                .WithOne(u => u.BirimSorumlusu)
                .HasForeignKey<BirimSorumlusu>(bs => bs.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            
            builder.Entity<GunlukGorev>()
                .HasOne(g => g.Stajyer)
                .WithMany(s => s.GunlukGorevler)
                .HasForeignKey(g => g.StajyerId)
                .OnDelete(DeleteBehavior.Cascade);

            
            builder.Entity<PerformansSkoru>()
                .HasOne(p => p.Stajyer)
                .WithMany(s => s.PerformansSkorlari)
                .HasForeignKey(p => p.StajyerId)
                .OnDelete(DeleteBehavior.Cascade);

            
            builder.Entity<StajyerProje>()
                .HasOne(sp => sp.Stajyer)
                .WithMany(s => s.Projeler)
                .HasForeignKey(sp => sp.StajyerId)
                .OnDelete(DeleteBehavior.Cascade);

            
            builder.Entity<StajyerYetenek>()
                .HasOne(sy => sy.Stajyer)
                .WithMany(s => s.Yetenekler)
                .HasForeignKey(sy => sy.StajyerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StajyerYetenek>()
                .HasOne(sy => sy.Yetenek)
                .WithMany(y => y.StajyerYetenekler)
                .HasForeignKey(sy => sy.YetenekId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EtkinlikKayit>()
                .HasOne(ek => ek.Etkinlik)
                .WithMany(e => e.Kayitlar)
                .HasForeignKey(ek => ek.EtkinlikId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EtkinlikKayit>()
                .HasOne(ek => ek.User)
                .WithMany()
                .HasForeignKey(ek => ek.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ManuelGorev>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            
            builder.Entity<GunlukGorev>()
                .HasIndex(g => new { g.StajyerId, g.Tarih })
                .IsUnique();

            builder.Entity<StajyerYetenek>()
                .HasIndex(sy => new { sy.StajyerId, sy.YetenekId })
                .IsUnique();

            
            builder.Entity<StajyerDetayView>()
                .ToView("vw_StajyerlerDetay")
                .HasNoKey();

            builder.Entity<BirimSorumlusuDetayView>()
                .ToView("vw_BirimSorumlulariDetay")
                .HasNoKey();
        }
    }
}
