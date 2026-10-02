namespace stajyer_takip.Services;

public interface IChatAssistantService
{
    Task<ChatAssistantResult> AskAsync(ChatAssistantRequest request, CancellationToken cancellationToken = default);
}

public sealed class ChatAssistantRequest
{
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Role { get; set; }
    public List<ChatMessageItem> History { get; set; } = new();
}

public sealed class ChatAssistantResult
{
    public string Answer { get; set; } = string.Empty;
    public List<string> Suggestions { get; set; } = new();
    public List<ChatLinkItem> Links { get; set; } = new();
}

public sealed class ChatMessageItem
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class ChatLinkItem
{
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

