using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;

namespace stajyer_takip.Services;

public class ChatAssistantService : IChatAssistantService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatAssistantService> _logger;

    public ChatAssistantService(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ChatAssistantService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ChatAssistantResult> AskAsync(ChatAssistantRequest request, CancellationToken cancellationToken = default)
    {
        var sanitizedMessage = (request.Message ?? string.Empty).Trim();
        var role = (request.Role ?? "Anonim").Trim();
        var isStajyer = role.Equals("Stajyer", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(sanitizedMessage))
        {
            return new ChatAssistantResult
            {
                Answer = "Sorunuzu biraz daha detaylandırabilir misiniz?",
                Suggestions = DefaultSuggestions(role),
                Links = DefaultLinks(role)
            };
        }

        var contextText = await BuildUserContextAsync(request.UserId, isStajyer, cancellationToken);
        var apiKey = _configuration["OpenAI:ApiKey"];
        var model = _configuration["OpenAI:Model"] ?? "gpt-4.1-mini";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return RuleBasedFallback(sanitizedMessage, contextText, role);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var systemPrompt =
                "Sen Belediye Asistanısın. " +
                "Sadece verilen bağlamdaki gerçek bilgilere dayanarak cevap ver. " +
                "Bilinmeyen durumda tahmin yapma, kullanıcıyı doğru menüye yönlendir. " +
                "Cevaplar Türkçe, kısa ve net olsun . " +
                "Uygun olduğunda ilgili sayfa adını belirt (örnek: Etkinlikler, Görevlerim, Bilgilerim).";

            var historyText = BuildHistoryText(request.History);
            var userPrompt = $"Kullanıcı rolü: {role}\nKullanıcı sorusu:\n{sanitizedMessage}\n\nSon sohbet özeti:\n{historyText}\n\nSistem bağlamı:\n{contextText}";

            var requestBody = new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI chat isteği başarısız: {StatusCode}", response.StatusCode);
                return RuleBasedFallback(sanitizedMessage, contextText, role);
            }

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(responseText);
            var answer = json.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return new ChatAssistantResult
            {
                Answer = string.IsNullOrWhiteSpace(answer)
                    ? "Şu an yanıt üretilemedi. Lütfen tekrar deneyin."
                    : answer.Trim(),
                Suggestions = DefaultSuggestions(role),
                Links = SuggestLinksByQuestion(sanitizedMessage, role)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatAssistantService.AskAsync sırasında hata oluştu.");
            return RuleBasedFallback(sanitizedMessage, contextText, role);
        }
    }

    private async Task<string> BuildUserContextAsync(string? userId, bool isStajyer, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Kullanıcı ID: {(string.IsNullOrWhiteSpace(userId) ? "-" : userId)}");

        var etkinlikler = await _context.Etkinlikler
            .Where(e => e.Aktif && e.Tarih >= DateTime.Now.Date.AddDays(-1))
            .OrderBy(e => e.Tarih)
            .Take(5)
            .Select(e => $"{e.Ad} ({e.Tarih:dd.MM.yyyy HH:mm})")
            .ToListAsync(cancellationToken);

        if (etkinlikler.Count > 0)
        {
            sb.AppendLine("Yaklaşan Etkinlikler:");
            foreach (var e in etkinlikler)
            {
                sb.AppendLine($"- {e}");
            }
        }

        if (!isStajyer || string.IsNullOrWhiteSpace(userId))
        {
            return sb.ToString();
        }

        var stajyer = await _context.Stajyerler
            .Include(s => s.Birim)
            .Include(s => s.Yetenekler)
                .ThenInclude(y => y.Yetenek)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (stajyer == null)
        {
            sb.AppendLine("Stajyer kaydı bulunamadı.");
            return sb.ToString();
        }

        var kalanGun = 0;
        if (stajyer.BitisTarihi != default(DateTime) && stajyer.BitisTarihi.Year >= 2000)
        {
            var bugun = DateTime.Now.Date;
            var tarih = bugun.AddDays(1);
            while (tarih <= stajyer.BitisTarihi.Date)
            {
                if (tarih.DayOfWeek != DayOfWeek.Saturday && tarih.DayOfWeek != DayOfWeek.Sunday)
                {
                    kalanGun++;
                }
                tarih = tarih.AddDays(1);
            }
        }

        var gorevler = await _context.GunlukGorevler
            .Where(g => g.StajyerId == stajyer.Id)
            .OrderByDescending(g => g.Tarih)
            .Take(5)
            .Select(g => $"{g.Tarih:dd.MM.yyyy}: {(g.Onaylandi ? "Onaylandı" : "Onay Bekliyor")}")
            .ToListAsync(cancellationToken);

        sb.AppendLine($"Birim: {stajyer.Birim?.Ad ?? "-"}");
        sb.AppendLine($"Kalan iş günü: {kalanGun}");
        sb.AppendLine($"Devam oranı: %{stajyer.DevamOrani:F1}");

        if (stajyer.Yetenekler.Any())
        {
            sb.AppendLine("Yetenekler:");
            foreach (var y in stajyer.Yetenekler.OrderByDescending(x => x.Seviye).Take(8))
            {
                sb.AppendLine($"- {y.Yetenek?.Ad} (Seviye: {y.Seviye}/5)");
            }
        }

        if (gorevler.Count > 0)
        {
            sb.AppendLine("Son Görevler:");
            foreach (var g in gorevler)
            {
                sb.AppendLine($"- {g}");
            }
        }

        return sb.ToString();
    }

    private static ChatAssistantResult RuleBasedFallback(string message, string contextText, string role)
    {
        var lower = message.ToLowerInvariant();
        var isStajyer = role.Equals("Stajyer", StringComparison.OrdinalIgnoreCase);

        if (lower.Contains("kalan") && lower.Contains("gün") && isStajyer)
        {
            return new ChatAssistantResult
            {
                Answer = $"Stajyer panelinizdeki güncel bilgiye göre kalan iş günü bilgisi hesaplanır. Özet:\n{ExtractLine(contextText, "Kalan iş günü:")}",
                Suggestions = DefaultSuggestions(role),
                Links = new List<ChatLinkItem>
                {
                    new() { Text = "Stajyer Paneli", Url = "/Stajyer/Index" },
                    new() { Text = "Bilgilerim", Url = "/Stajyer/Bilgilerim" }
                }
            };
        }

        if (lower.Contains("etkinlik"))
        {
            return new ChatAssistantResult
            {
                Answer = "Etkinlikler için üst menüdeki Etkinlikler sayfasına gidebilirsiniz. Yaklaşan etkinlikler burada listelenir.",
                Suggestions = new List<string> { "Yaklaşan etkinlikler", "Etkinliğe nasıl kayıt olurum?", "Etkinlik detay" },
                Links = new List<ChatLinkItem>
                {
                    new() { Text = "Etkinlikler", Url = "/Etkinlikler" }
                }
            };
        }

        return new ChatAssistantResult
        {
            Answer = "Sorunuzu aldım. Size en doğru yanıt için paneldeki verilerinizi ve etkinlik bilgilerini kullanıyorum. Daha net bir sonuç için sorunuzu biraz daha spesifik yazabilirsiniz.",
            Suggestions = DefaultSuggestions(role),
            Links = DefaultLinks(role)
        };
    }

    private static string ExtractLine(string text, string prefix)
    {
        var line = text.Split('\n').FirstOrDefault(x => x.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(line) ? prefix + " bilgi bulunamadı." : line.Trim();
    }

    private static string BuildHistoryText(List<ChatMessageItem> history)
    {
        if (history == null || history.Count == 0)
        {
            return "Sohbet geçmişi yok.";
        }

        var last = history.TakeLast(6)
            .Select(h => $"{h.Role}: {h.Content}")
            .ToList();
        return string.Join("\n", last);
    }

    private static List<string> DefaultSuggestions(string role)
    {
        if (role.Equals("Stajyer", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string>
            {
                "Kalan günüm kaç?",
                "Son görev durumum nedir?",
                "Yaklaşan etkinlikler neler?"
            };
        }

        if (role.Equals("IK", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string>
            {
                "Stajyer ekleme adımları",
                "Birim yönetimi nerede?",
                "Yetenek haritasına nasıl giderim?"
            };
        }

        if (role.Equals("BirimSorumlusu", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string>
            {
                "Birim yetenek haritası",
                "Etkinlik ekleme",
                "Stajyer onayları"
            };
        }

        return new List<string>
        {
            "Yaklaşan etkinlikler",
            "Giriş nasıl yapılır?",
            "Sistemde bugün neler var?"
        };
    }

    private static List<ChatLinkItem> DefaultLinks(string role)
    {
        var links = new List<ChatLinkItem>
        {
            new() { Text = "Ana Sayfa", Url = "/" },
            new() { Text = "Etkinlikler", Url = "/Etkinlikler" }
        };

        if (role.Equals("Stajyer", StringComparison.OrdinalIgnoreCase))
        {
            links.Add(new ChatLinkItem { Text = "Görevlerim", Url = "/Stajyer/Gorevlerim" });
            links.Add(new ChatLinkItem { Text = "Bilgilerim", Url = "/Stajyer/Bilgilerim" });
        }
        else if (role.Equals("IK", StringComparison.OrdinalIgnoreCase))
        {
            links.Add(new ChatLinkItem { Text = "Birim Yönetimi", Url = "/IK/BirimYonetimi" });
            links.Add(new ChatLinkItem { Text = "Stajyer Ekle", Url = "/IK/StajyerEkle" });
        }
        else if (role.Equals("BirimSorumlusu", StringComparison.OrdinalIgnoreCase))
        {
            links.Add(new ChatLinkItem { Text = "Yetenek Haritası", Url = "/BirimSorumlusu/BirimYetenekHaritasi" });
        }
        else if (role.Equals("ITYoneticisi", StringComparison.OrdinalIgnoreCase))
        {
            links.Add(new ChatLinkItem { Text = "Kullanıcı Yönetimi", Url = "/ITYoneticisi/KullaniciYonetimi" });
        }
        else
        {
            links.Add(new ChatLinkItem { Text = "Giriş Yap", Url = "/Account/Login" });
        }

        return links;
    }

    private static List<ChatLinkItem> SuggestLinksByQuestion(string message, string role)
    {
        var lower = message.ToLowerInvariant();
        if (lower.Contains("etkinlik"))
        {
            return new List<ChatLinkItem> { new() { Text = "Etkinlikler", Url = "/Etkinlikler" } };
        }

        if (lower.Contains("görev"))
        {
            if (role.Equals("Stajyer", StringComparison.OrdinalIgnoreCase))
            {
                return new List<ChatLinkItem>
                {
                    new() { Text = "Görevlerim", Url = "/Stajyer/Gorevlerim" },
                    new() { Text = "Görev Ekle", Url = "/Stajyer/GunlukGorevEkle" }
                };
            }
        }

        if (lower.Contains("profil") || lower.Contains("bilgi"))
        {
            return new List<ChatLinkItem> { new() { Text = "Bilgilerim", Url = "/Stajyer/Bilgilerim" } };
        }

        return DefaultLinks(role);
    }
}

