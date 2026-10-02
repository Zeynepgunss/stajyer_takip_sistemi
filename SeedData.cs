using Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccessLayer
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                
                await context.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                
                Console.WriteLine($"Migration hatası: {ex.Message}");
                throw;
            }

            
            string[] roles = { "Stajyer", "BirimSorumlusu", "IK", "ITYoneticisi" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Varsayılan yetenekleri oluştur
            if (!await context.Yetenekler.AnyAsync())
            {
                var yetenekler = new List<Yetenek>
                {
                    new Yetenek { Ad = "C#", Kategori = "Backend" },
                    new Yetenek { Ad = "SQL", Kategori = "Database" },
                    new Yetenek { Ad = "React", Kategori = "Frontend" },
                    new Yetenek { Ad = "JavaScript", Kategori = "Frontend" },
                    new Yetenek { Ad = "ASP.NET Core", Kategori = "Backend" },
                    new Yetenek { Ad = "Entity Framework", Kategori = "Backend" },
                    new Yetenek { Ad = "AI/ML", Kategori = "AI" },
                    new Yetenek { Ad = "Python", Kategori = "Backend" },
                    new Yetenek { Ad = "Vue.js", Kategori = "Frontend" },
                    new Yetenek { Ad = "Angular", Kategori = "Frontend" }
                };

                await context.Yetenekler.AddRangeAsync(yetenekler);
                await context.SaveChangesAsync();
            }

            
            if (!await context.Birimler.AnyAsync())
            {
                var birimler = new List<Birim>
                {
                    new Birim { Ad = "Bilgi İşlem", Kontenjan = 5, MevcutStajyerSayisi = 0 },
                    new Birim { Ad = "İnsan Kaynakları", Kontenjan = 3, MevcutStajyerSayisi = 0 },
                    new Birim { Ad = "Mali İşler", Kontenjan = 4, MevcutStajyerSayisi = 0 },
                    new Birim { Ad = "Planlama ve Proje", Kontenjan = 3, MevcutStajyerSayisi = 0 }
                };

                await context.Birimler.AddRangeAsync(birimler);
                await context.SaveChangesAsync();
            }

            // Varsayılan etkinlikleri oluştur veya güncelle (etkinlik tarihleri sabit DateTime ile tutulur)
            try
            {
                var varsayilanEtkinlikler = new List<Etkinlik>
                {
                    new Etkinlik { Ad = "Dijital Dönüşüm Günleri", GorselDosyaAdi = "etkinlik.png", Aciklama = "Bu etkinlik belediyemizin dijital dönüşüm vizyonunu tanıtmak amacıyla düzenlenmektedir. Katılımcılar belediyenin yürüttüğü dijital projeler hakkında kapsamlı bilgi edinecektir. E-belediye hizmetlerinin geliştirilmesi ve vatandaş odaklı çözümler ele alınacaktır. Veri yönetimi, yapay zeka ve otomasyon sistemlerinin kamu hizmetlerine etkisi anlatılacaktır. Alanında uzman konuşmacılar deneyimlerini paylaşacaktır. Gerçek uygulama örnekleri ve başarı hikayeleri sunulacaktır. Katılımcılar soru cevap bölümü ile uzmanlara doğrudan sorular yöneltebilecektir. Etkinlik sonunda dijital belediyecilik alanındaki gelecek hedefleri değerlendirilecektir.", Tarih = new DateTime(2026, 3, 2), Saat = "17.00", Yer = "Belediye Konferans Salonu", Kontenjan = 100 },
                    new Etkinlik { Ad = "Akıllı Şehirler Teknoloji Zirvesi", GorselDosyaAdi = "etkinlik.png", Aciklama = "Bu zirve akıllı şehir teknolojileri konusunda farkındalık oluşturmayı amaçlamaktadır. Şehirlerde kullanılan IoT tabanlı sistemler detaylı şekilde ele alınacaktır. Akıllı ulaşım çözümleri ve trafik yönetimi teknolojileri anlatılacaktır. Enerji verimliliği sağlayan akıllı altyapı projeleri tanıtılacaktır. Sürdürülebilir şehir planlaması üzerine uzman sunumları gerçekleştirilecektir. Akademisyenler ve sektör temsilcileri deneyimlerini paylaşacaktır. Katılımcılar şehir teknolojilerindeki yeni trendleri öğrenme fırsatı bulacaktır. Etkinlik sonunda akıllı şehir projelerinin geleceği tartışılacaktır.", Tarih = new DateTime(2026, 3, 12), Saat = "10:00", Yer = "Kongre Merkezi", Kontenjan = 150 },
                    new Etkinlik { Ad = "Yerel Yönetimlerde Yazılım Çalıştayı", GorselDosyaAdi = "etkinlik.png", Aciklama = "Bu çalıştay yerel yönetimlerde kullanılan yazılım sistemlerine odaklanmaktadır. Kamu kurumlarında geliştirilen yazılımların mimari yapıları incelenecektir. Güvenli yazılım geliştirme süreçleri detaylı olarak anlatılacaktır. Katılımcılar uygulamalı örnekler üzerinden deneyim kazanacaktır. Yazılım projelerinde performans ve ölçeklenebilirlik konuları ele alınacaktır. Modern yazılım geliştirme araçları ve metodolojileri tanıtılacaktır. Katılımcılar ekipler halinde çözüm geliştirme çalışmaları yapacaktır. Çalıştay sonunda katılımcıların geliştirdiği fikirler paylaşılacaktır.", Tarih = new DateTime(2026, 3, 16), Saat = "14:00", Yer = "Eğitim Salonu A", Kontenjan = 50 },
                    new Etkinlik { Ad = "Kamu Bilişim Buluşmaları", GorselDosyaAdi = "etkinlik.png", Aciklama = "Bu etkinlik kamu kurumlarında yürütülen bilişim projelerini bir araya getirmeyi amaçlamaktadır. Farklı kurumların teknoloji projeleri katılımcılarla paylaşılacaktır. Kamu bilişim altyapısının geliştirilmesi üzerine sunumlar yapılacaktır. Siber güvenlik ve veri koruma konuları detaylı şekilde ele alınacaktır. Kurumlar arası iş birliğinin artırılması hedeflenmektedir. Katılımcılar deneyimlerini paylaşma fırsatı bulacaktır. Yeni projeler için ortak çalışma imkanları değerlendirilecektir. Etkinlik sonunda kamu bilişim projelerinin geleceği tartışılacaktır.", Tarih = new DateTime(2026, 3, 19), Saat = "09:30", Yer = "Belediye Toplantı Salonu", Kontenjan = 80 },
                    new Etkinlik { Ad = "Dijital Belediyecilik Forumu", GorselDosyaAdi = "etkinlik.png", Aciklama = "Bu forum dijital belediyecilik uygulamalarını kapsamlı şekilde ele almayı hedeflemektedir. Vatandaşlara sunulan çevrim içi hizmetler detaylı olarak anlatılacaktır. Mobil belediye uygulamaları ve dijital platformlar tanıtılacaktır. Belediyelerde veri yönetimi ve analiz süreçleri ele alınacaktır. Uzman konuşmacılar başarılı projeleri paylaşacaktır. Katılımcılar forum kapsamında görüşlerini paylaşabilecektir. Panel oturumları ile farklı bakış açıları değerlendirilecektir. Etkinlik sonunda dijital belediyecilik alanındaki yeni fırsatlar konuşulacaktır.", Tarih = new DateTime(2026, 3, 23), Saat = "11:00", Yer = "Şehir Kütüphanesi Konferans Salonu", Kontenjan = 120 },
                    new Etkinlik { Ad = "Kent Teknolojileri Zirvesi", GorselDosyaAdi = "etkinlik.png", Aciklama = "Bu zirve kent yönetiminde kullanılan yeni teknolojilere odaklanmaktadır. Büyük veri ve yapay zeka tabanlı şehir sistemleri tanıtılacaktır. Akıllı altyapı çözümleri ve dijital yönetim araçları ele alınacaktır. Kent planlama süreçlerinde teknolojinin rolü anlatılacaktır. Uzman konuşmacılar şehir inovasyonu hakkında sunum yapacaktır. Katılımcılar yeni teknolojileri yakından tanıma fırsatı bulacaktır. Şehir yaşamını kolaylaştıran projeler detaylı şekilde paylaşılacaktır. Zirve sonunda kent teknolojilerinin geleceği değerlendirilecektir.", Tarih = new DateTime(2026, 3, 27), Saat = "10:00", Yer = "Belediye Konferans Salonu", Kontenjan = 100 },
                    new Etkinlik { Ad = "Yenilikçi Çözümler Hackathonu", GorselDosyaAdi = "etkinlik.png", Aciklama = "Bu hackathon belediye sorunlarına teknolojik çözümler üretmek amacıyla düzenlenmektedir. Katılımcılar ekipler halinde yazılım projeleri geliştirecektir. Yarışma süresince mentorlar katılımcılara destek verecektir. Gerçek belediye problemleri üzerine çözüm fikirleri geliştirilecektir. Katılımcılar yaratıcı projeler üretme fırsatı bulacaktır. Jüri üyeleri projeleri belirli kriterlere göre değerlendirecektir. Başarılı ekipler ödüller kazanacaktır. Hackathon sonunda ortaya çıkan projeler belediye tarafından değerlendirilecektir.", Tarih = new DateTime(2026, 3, 30), Saat = "09:00", Yer = "Bilgi İşlem Merkezi", Kontenjan = 40 },
                    new Etkinlik { Ad = "Belediye Bilişim Günleri", GorselDosyaAdi = "etkinlik.png", Aciklama = "Bu etkinlik belediyenin bilişim altyapısını ve teknoloji projelerini tanıtmak amacıyla düzenlenmektedir. Katılımcılar mevcut dijital sistemler hakkında bilgi edinecektir. Belediye tarafından geliştirilen yazılım projeleri tanıtılacaktır. Teknoloji ekipleri yürüttükleri çalışmaları paylaşacaktır. Uygulamalı demo gösterimleri gerçekleştirilecektir. Katılımcılar stant alanlarında projeleri inceleyebilecektir. Yeni teknoloji yatırımları hakkında bilgiler verilecektir. Etkinlik sonunda katılımcılarla fikir alışverişi yapılacaktır.", Tarih = new DateTime(2026, 4, 3), Saat = "13:00", Yer = "Kongre Merkezi", Kontenjan = 200 },
                    new Etkinlik { Ad = "Şehir ve Teknoloji Sempozyumu", GorselDosyaAdi = "etkinlik.png", Aciklama = "Bu sempozyum şehir planlama ve teknoloji entegrasyonu konularını ele almaktadır. Akademisyenler ve sektör uzmanları araştırmalarını paylaşacaktır. Akıllı şehir projeleri ve sürdürülebilir çözümler tartışılacaktır. Teknolojinin şehir yaşamına katkıları değerlendirilecektir. Katılımcılar farklı oturumlarda bildiriler dinleyebilecektir. Panel oturumlarında yeni fikirler ortaya konacaktır. Şehir yönetiminde teknolojinin geleceği konuşulacaktır. Sempozyum sonunda genel değerlendirme yapılacaktır.", Tarih = new DateTime(2026, 4, 8), Saat = "15:00", Yer = "Belediye Konferans Salonu", Kontenjan = 90 }
                };
                var mevcutEtkinlikler = await context.Etkinlikler.ToListAsync();
                foreach (var varsayilan in varsayilanEtkinlikler)
                {
                    var mevcut = mevcutEtkinlikler.FirstOrDefault(e => e.Ad == varsayilan.Ad);
                    if (mevcut != null)
                    {
                        mevcut.Aciklama = varsayilan.Aciklama;
                        mevcut.Tarih = varsayilan.Tarih;
                        mevcut.Saat = varsayilan.Saat;
                        mevcut.Yer = varsayilan.Yer;
                        mevcut.Kontenjan = varsayilan.Kontenjan;
                        mevcut.GorselDosyaAdi = varsayilan.GorselDosyaAdi;
                    }
                    else
                    {
                        context.Etkinlikler.Add(varsayilan);
                    }
                }
                await context.SaveChangesAsync();
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                
            }
        }
    }
}
