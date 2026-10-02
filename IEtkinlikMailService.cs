namespace stajyer_takip.Services;

public interface IEtkinlikMailService
{
    /// <summary>
    /// Etkinlik güncellendiğinde birimden kayıt olan katılımcılara bilgi maili gönderir.
    /// Gönderilen alıcı sayısını döner (0 = gönderilmedi veya hata).
    /// </summary>
    Task<int> GuncellemeBildirimiGonderAsync(Entity.Etkinlik etkinlik, int birimId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Etkinlik güncellendiğinde etkinliğe kayıtlı tüm katılımcılara bilgi maili gönderir (IT Yöneticisi için).
    /// </summary>
    Task<int> GuncellemeBildirimiTumKatilimcilaraGonderAsync(Entity.Etkinlik etkinlik, CancellationToken cancellationToken = default);

    /// <summary>
    /// Etkinlik silindiğinde kayıtlı alıcılara bilgilendirme maili gönderir.
    /// Gönderilen alıcı sayısını döner (0 = gönderilmedi veya hata).
    /// </summary>
    Task<int> SilindiBildirimiGonderAsync(string etkinlikAdi, IReadOnlyList<string> aliciEpostalari, CancellationToken cancellationToken = default);
}
