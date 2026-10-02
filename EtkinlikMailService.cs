using System.Net;
using System.Net.Mail;
using DataAccessLayer;
using Entity;
using Microsoft.EntityFrameworkCore;

namespace stajyer_takip.Services;

public class EtkinlikMailService : IEtkinlikMailService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<EtkinlikMailService> _logger;

    public EtkinlikMailService(
        ApplicationDbContext context,
        IConfiguration config,
        ILogger<EtkinlikMailService> logger)
    {
        _context = context;
        _config = config;
        _logger = logger;
    }

    public async Task<int> GuncellemeBildirimiGonderAsync(Etkinlik etkinlik, int birimId, CancellationToken cancellationToken = default)
    {
        var from = _config["Mail:From"];
        var appPassword = _config["Mail:AppPassword"];
        var host = _config["Mail:SmtpHost"] ?? "smtp.gmail.com";
        var port = int.TryParse(_config["Mail:SmtpPort"], out var p) ? p : 587;
        var userName = _config["Mail:UserName"] ?? from;

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(appPassword))
        {
            _logger.LogWarning("Mail ayarları (From veya AppPassword) eksik; etkinlik güncelleme bildirimi gönderilmedi. appsettings.Development.json içinde Mail:From ve Mail:AppPassword doldurun.");
            return 0;
        }

        // Birimdeki aktif stajyerlerin UserId listesi
        var birimdenStajyerUserIds = await _context.Stajyerler
            .Where(s => s.BirimId == birimId && s.Aktif)
            .Select(s => s.UserId)
            .ToListAsync(cancellationToken);

        if (birimdenStajyerUserIds.Count == 0)
        {
            _logger.LogInformation("Birim {BirimId} için aktif stajyer bulunamadı; mail gönderilmedi.", birimId);
            return 0;
        }

        // Bu etkinliğe kayıtlı ve yukarıdaki listede olan katılımcılar (e-posta için User include)
        var kayitlar = await _context.EtkinlikKayitlari
            .Include(k => k.User)
            .Where(k => k.EtkinlikId == etkinlik.Id && k.UserId != null && birimdenStajyerUserIds.Contains(k.UserId))
            .ToListAsync(cancellationToken);

        var alicilar = new List<string>();
        foreach (var k in kayitlar)
        {
            var email = (k.KatilimciEmail ?? k.User?.Email)?.Trim();
            if (!string.IsNullOrWhiteSpace(email))
                alicilar.Add(email);
        }

        alicilar = alicilar.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (alicilar.Count == 0)
        {
            _logger.LogInformation("Etkinlik {EtkinlikId} için birim {BirimId} içinde e-posta gönderilecek kayıtlı katılımcı bulunamadı.", etkinlik.Id, birimId);
            return 0;
        }

        var konu = $"Etkinlik Güncellendi: {etkinlik.Ad}";
        var govde = EtkinlikGuncellemeSablonu(etkinlik);

        try
        {
            var sifre = appPassword.Trim().Replace(" ", "");
            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(userName, sifre)
            };

            var mail = new MailMessage
            {
                From = new MailAddress(from.Trim(), "Stajyer Takip Sistemi"),
                Subject = konu,
                Body = govde,
                IsBodyHtml = true
            };

            foreach (var adres in alicilar)
            {
                if (!string.IsNullOrWhiteSpace(adres))
                    mail.To.Add(adres);
            }

            await smtp.SendMailAsync(mail, cancellationToken);
            _logger.LogInformation("Etkinlik güncelleme bildirimi {Count} kişiye gönderildi: {EtkinlikAd}", alicilar.Count, etkinlik.Ad);
            return alicilar.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Etkinlik güncelleme maili gönderilirken hata: EtkinlikId={EtkinlikId}, BirimId={BirimId}", etkinlik.Id, birimId);
            return 0;
        }
    }

    public async Task<int> GuncellemeBildirimiTumKatilimcilaraGonderAsync(Etkinlik etkinlik, CancellationToken cancellationToken = default)
    {
        var from = _config["Mail:From"];
        var appPassword = _config["Mail:AppPassword"];
        var host = _config["Mail:SmtpHost"] ?? "smtp.gmail.com";
        var port = int.TryParse(_config["Mail:SmtpPort"], out var p) ? p : 587;
        var userName = _config["Mail:UserName"] ?? from;

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(appPassword))
        {
            _logger.LogWarning("Mail ayarları eksik; etkinlik güncelleme bildirimi (tüm katılımcılar) gönderilmedi.");
            return 0;
        }

        var kayitlar = await _context.EtkinlikKayitlari
            .Include(k => k.User)
            .Where(k => k.EtkinlikId == etkinlik.Id)
            .ToListAsync(cancellationToken);

        var alicilar = new List<string>();
        foreach (var k in kayitlar)
        {
            var email = (k.KatilimciEmail ?? k.User?.Email)?.Trim();
            if (!string.IsNullOrWhiteSpace(email))
                alicilar.Add(email);
        }

        alicilar = alicilar.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (alicilar.Count == 0)
        {
            _logger.LogInformation("Etkinlik {EtkinlikId} için e-posta gönderilecek kayıtlı katılımcı bulunamadı.", etkinlik.Id);
            return 0;
        }

        var konu = $"Etkinlik Güncellendi: {etkinlik.Ad}";
        var govde = EtkinlikGuncellemeSablonu(etkinlik);

        try
        {
            var sifre = appPassword.Trim().Replace(" ", "");
            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(userName, sifre)
            };

            var mail = new MailMessage
            {
                From = new MailAddress(from.Trim(), "Stajyer Takip Sistemi"),
                Subject = konu,
                Body = govde,
                IsBodyHtml = true
            };

            foreach (var adres in alicilar)
            {
                if (!string.IsNullOrWhiteSpace(adres))
                    mail.To.Add(adres);
            }

            await smtp.SendMailAsync(mail, cancellationToken);
            _logger.LogInformation("Etkinlik güncelleme bildirimi (tüm katılımcılar) {Count} kişiye gönderildi: {EtkinlikAd}", alicilar.Count, etkinlik.Ad);
            return alicilar.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Etkinlik güncelleme maili (tüm katılımcılar) gönderilirken hata: EtkinlikId={EtkinlikId}", etkinlik.Id);
            return 0;
        }
    }

    public async Task<int> SilindiBildirimiGonderAsync(string etkinlikAdi, IReadOnlyList<string> aliciEpostalari, CancellationToken cancellationToken = default)
    {
        if (aliciEpostalari == null || aliciEpostalari.Count == 0)
            return 0;

        var from = _config["Mail:From"];
        var appPassword = _config["Mail:AppPassword"];
        var host = _config["Mail:SmtpHost"] ?? "smtp.gmail.com";
        var port = int.TryParse(_config["Mail:SmtpPort"], out var p) ? p : 587;
        var userName = _config["Mail:UserName"] ?? from;

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(appPassword))
        {
            _logger.LogWarning("Mail ayarları eksik; etkinlik silindi bildirimi gönderilmedi.");
            return 0;
        }

        var alicilar = aliciEpostalari
            .Select(e => e?.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (alicilar.Count == 0)
            return 0;

        var konu = $"Etkinlik Bilgisi: {System.Net.WebUtility.HtmlEncode(etkinlikAdi)}";
        var govde = EtkinlikSilindiSablonu(etkinlikAdi);

        try
        {
            var sifre = appPassword.Trim().Replace(" ", "");
            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(userName, sifre)
            };

            var mail = new MailMessage
            {
                From = new MailAddress(from.Trim(), "Stajyer Takip Sistemi"),
                Subject = konu,
                Body = govde,
                IsBodyHtml = true
            };

            foreach (var adres in alicilar)
            {
                if (!string.IsNullOrWhiteSpace(adres))
                    mail.To.Add(adres);
            }

            await smtp.SendMailAsync(mail, cancellationToken);
            _logger.LogInformation("Etkinlik silindi bildirimi {Count} kişiye gönderildi: {EtkinlikAd}", alicilar.Count, etkinlikAdi);
            return alicilar.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Etkinlik silindi bildirimi maili gönderilirken hata: {EtkinlikAd}", etkinlikAdi);
            return 0;
        }
    }

    private static string EtkinlikSilindiSablonu(string etkinlikAdi)
    {
        var ad = System.Net.WebUtility.HtmlEncode(etkinlikAdi);
        return $@"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8"" /></head>
<body style=""font-family: Segoe UI, Arial, sans-serif; line-height: 1.6; color: #333;"">
  <div style=""max-width: 600px; margin: 0 auto; padding: 24px;"">
    <h2 style=""color: #2a5298;"">Etkinlik Hakkında Bilgilendirme</h2>
    <p>Sayın katılımcı,</p>
    <p><strong>{ad}</strong> etkinliğiyle ilgili bilgilendirme yapmak isteriz.</p>
    <p>Etkinlik tarihi belirsiz bir tarihe ertelenmiştir. Güncel duyuruları ve yeni tarihi <strong>Etkinlikler</strong> kısmından takip edebilirsiniz.</p>
    <p>Anlayışınız için teşekkür ederiz.</p>
    <p style=""margin-top: 32px; color: #888; font-size: 12px;"">Bu e-posta Stajyer Takip Sistemi tarafından otomatik gönderilmiştir.</p>
  </div>
</body>
</html>";
    }

    private static string EtkinlikGuncellemeSablonu(Etkinlik e)
    {
        var tarihStr = e.Tarih.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        var bitisStr = e.BitisTarihi.HasValue
            ? e.BitisTarihi.Value.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))
            : "-";
        var yer = string.IsNullOrWhiteSpace(e.Yer) ? "-" : e.Yer;

        return $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8"" /></head>
<body style=""font-family: Segoe UI, Arial, sans-serif; line-height: 1.5; color: #333;"">
  <div style=""max-width: 600px; margin: 0 auto; padding: 20px;"">
    <h2 style=""color: #2a5298;"">Etkinlik Bilgisi Güncellendi</h2>
    <p>Kayıt olduğunuz etkinliğin bilgileri güncellenmiştir.</p>
    <table style=""width: 100%; border-collapse: collapse; margin: 20px 0;"">
      <tr><td style=""padding: 8px; border: 1px solid #ddd; background: #f9f9f9; font-weight: bold;"">Etkinlik</td><td style=""padding: 8px; border: 1px solid #ddd;"">{System.Net.WebUtility.HtmlEncode(e.Ad)}</td></tr>
      <tr><td style=""padding: 8px; border: 1px solid #ddd; background: #f9f9f9; font-weight: bold;"">Başlangıç</td><td style=""padding: 8px; border: 1px solid #ddd;"">{tarihStr}</td></tr>
      <tr><td style=""padding: 8px; border: 1px solid #ddd; background: #f9f9f9; font-weight: bold;"">Bitiş</td><td style=""padding: 8px; border: 1px solid #ddd;"">{bitisStr}</td></tr>
      <tr><td style=""padding: 8px; border: 1px solid #ddd; background: #f9f9f9; font-weight: bold;"">Yer</td><td style=""padding: 8px; border: 1px solid #ddd;"">{System.Net.WebUtility.HtmlEncode(yer)}</td></tr>
    </table>
    <p style=""color: #666; font-size: 14px;"">Detaylar için sisteme giriş yapıp etkinlik sayfasını kontrol edebilirsiniz.</p>
    <p style=""margin-top: 24px; color: #888; font-size: 12px;"">Bu e-posta Stajyer Takip Sistemi tarafından otomatik gönderilmiştir.</p>
  </div>
</body>
</html>";
    }
}
