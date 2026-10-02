using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using stajyer_takip.Services;

namespace stajyer_takip.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatAssistantService _chatAssistantService;

    public ChatController(IChatAssistantService chatAssistantService)
    {
        _chatAssistantService = chatAssistantService;
    }

    [HttpPost("ask")]
    [AllowAnonymous]
    public async Task<IActionResult> Ask([FromBody] ChatAskRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ChatAskResponse
            {
                Success = false,
                Answer = "Lütfen bir mesaj girin."
            });
        }

        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        var role = User.IsInRole("Stajyer")
            ? "Stajyer"
            : User.IsInRole("IK")
                ? "IK"
                : User.IsInRole("BirimSorumlusu")
                    ? "BirimSorumlusu"
                    : User.IsInRole("ITYoneticisi")
                        ? "ITYoneticisi"
                        : "Anonim";

        var result = await _chatAssistantService.AskAsync(new ChatAssistantRequest
        {
            UserId = userId,
            Role = role,
            Message = request.Message,
            History = request.History ?? new List<ChatMessageItem>()
        }, cancellationToken);

        return Ok(new ChatAskResponse
        {
            Success = true,
            Answer = result.Answer,
            Suggestions = result.Suggestions,
            Links = result.Links
        });
    }
}

public class ChatAskRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageItem>? History { get; set; }
}

public class ChatAskResponse
{
    public bool Success { get; set; }
    public string Answer { get; set; } = string.Empty;
    public List<string> Suggestions { get; set; } = new();
    public List<ChatLinkItem> Links { get; set; } = new();
}

