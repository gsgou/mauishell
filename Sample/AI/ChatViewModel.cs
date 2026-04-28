using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using OpenAI;
using Shiny;
using Shiny.Maui.Controls.Chat;

using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatMessage = Shiny.Maui.Controls.Chat.ChatMessage;

namespace Sample.AI;

[ShellMap<ChatPage>]
public partial class ChatViewModel(
    AiMauiShellTools aiTools,
    GitHubCopilotAuthService authService
) : ObservableObject, IPageLifecycleAware
{
    IChatClient? chatClient;
    CancellationTokenSource? cts;
    readonly List<AIChatMessage> history = [];

    static readonly ChatParticipant aiParticipant = new()
    {
        Id = "copilot",
        DisplayName = "AI"
    };

    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<ChatParticipant> TypingParticipants { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    bool isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        if (value)
            TypingParticipants.Add(aiParticipant);
        else
            TypingParticipants.Remove(aiParticipant);
    }

    [ObservableProperty]
    string authStatus = "Not authenticated";

    [ObservableProperty]
    string? userCode;

    [ObservableProperty]
    bool isAuthenticated;

    public bool IsNotBusy => !IsBusy;

    public async void OnAppearing()
    {
        if (IsAuthenticated || IsBusy)
            return;

        IsBusy = true;
        AuthStatus = "Restoring session...";

        try
        {
            if (await authService.TryRestoreSessionAsync())
            {
                SetupChatClient();
                IsAuthenticated = true;
                AuthStatus = "Ready to chat";
            }
            else
            {
                AuthStatus = "Not authenticated";
            }
        }
        catch
        {
            AuthStatus = "Not authenticated";
        }
        finally
        {
            IsBusy = false;
        }
    }
    public void OnDisappearing() => cts?.Cancel();

    [RelayCommand]
    async Task Login()
    {
        if (IsAuthenticated) return;

        try
        {
            IsBusy = true;
            AuthStatus = "Requesting device code...";
            cts = new CancellationTokenSource();

            var deviceCode = await authService.RequestDeviceCodeAsync(cts.Token);
            UserCode = deviceCode.UserCode;
            AuthStatus = $"Enter code {deviceCode.UserCode} at {deviceCode.VerificationUri}";

            await Browser.Default.OpenAsync(deviceCode.VerificationUri, BrowserLaunchMode.External);

            AuthStatus = "Waiting for authorization...";
            var success = await authService.PollForAccessTokenAsync(
                deviceCode.DeviceCode,
                deviceCode.Interval,
                cts.Token
            );

            if (success)
            {
                AuthStatus = "Authenticated! Setting up AI client...";
                SetupChatClient();
                IsAuthenticated = true;
                UserCode = null;
                AuthStatus = "Ready to chat";
            }
            else
            {
                AuthStatus = "Authentication failed or expired. Try again.";
            }
        }
        catch (OperationCanceledException)
        {
            AuthStatus = "Authentication cancelled.";
        }
        catch (Exception ex)
        {
            AuthStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    void SetupChatClient()
    {
        var transport = new CopilotTokenHandler(authService, new HttpClientHandler());

        var client = new OpenAIClient(
            new ApiKeyCredential("copilot-placeholder"),
            new OpenAIClientOptions
            {
                Transport = new HttpClientPipelineTransport(new HttpClient(transport)),
                Endpoint = new Uri("https://api.githubcopilot.com")
            }
        );

        chatClient = client
            .GetChatClient("gpt-4.1")
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        history.Clear();
        Debug.WriteLine($"[AI] AiRoutePrompt:\n{aiTools.Prompt}");
        history.Add(new AIChatMessage(ChatRole.System,
            $"""
            You are a helpful assistant integrated in a .NET MAUI app. You can navigate the user to pages and pre-fill forms using the NavigateToRoute tool.

            {aiTools.Prompt}
            When the user describes a problem, request, or intent that matches a route, call NavigateToRoute immediately with the appropriate route and parameters inferred from what the user said. Do not ask the user to confirm parameters unless something is genuinely ambiguous.

            When the user first greets you or asks what you can do, briefly describe your capabilities based on the available routes above.
            """));

        // Build a welcome message describing what the bot can do based on available routes
        var routes = aiTools.GetAiToolApplicableGeneratedRoutes();
        var capabilities = string.Join("\n", routes.Select(r => $"- {r.Description}"));
        Messages.Add(new ChatMessage
        {
            Text = $"Hi! I'm your AI assistant. Here's what I can help with:\n{capabilities}\n\nJust describe what you need and I'll take care of the rest!",
            IsFromMe = false,
            SenderId = "copilot"
        });
    }

    [RelayCommand]
    async Task Send(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || chatClient == null) return;

        Messages.Add(new ChatMessage
        {
            Text = text,
            IsFromMe = true,
            SenderId = "user"
        });

        try
        {
            IsBusy = true;
            cts = new CancellationTokenSource();

            Debug.WriteLine("[AI] Registered tools:");
            foreach (var tool in aiTools.Tools)
                Debug.WriteLine($"  - {tool}");

            var options = new ChatOptions { Tools = [.. aiTools.Tools] };

            // Send only the system prompt + the current user message (not full history)
            var messages = new List<AIChatMessage>
            {
                history[0], // system prompt
                new AIChatMessage(ChatRole.User, text)
            };
            var response = await chatClient.GetResponseAsync(messages, options, cts.Token);

            Debug.WriteLine($"[AI] Response messages: {response.Messages.Count}");
            foreach (var msg in response.Messages)
            {
                Debug.WriteLine($"[AI]   Role={msg.Role}, Contents={msg.Contents.Count}");
                foreach (var content in msg.Contents)
                {
                    switch (content)
                    {
                        case TextContent tc:
                            Debug.WriteLine($"[AI]     TextContent: {tc.Text?[..Math.Min(200, tc.Text?.Length ?? 0)]}");
                            break;
                        case FunctionCallContent fc:
                            Debug.WriteLine($"[AI]     FunctionCall: {fc.Name}({System.Text.Json.JsonSerializer.Serialize(fc.Arguments)})");
                            break;
                        case FunctionResultContent fr:
                            Debug.WriteLine($"[AI]     FunctionResult: CallId={fr.CallId}, Result={fr.Result}");
                            break;
                        default:
                            Debug.WriteLine($"[AI]     {content.GetType().Name}: {content}");
                            break;
                    }
                }
            }

            var assistantText = response.Text ?? "(no response)";
            Debug.WriteLine($"[AI] Final text: {assistantText}");

            Messages.Add(new ChatMessage
            {
                Text = assistantText,
                IsFromMe = false,
                SenderId = "copilot"
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AI] ERROR: {ex}");
            Messages.Add(new ChatMessage
            {
                Text = $"Error: {ex.Message}",
                IsFromMe = false,
                SenderId = "error"
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    void Logout()
    {
        authService.Logout();
        chatClient = null;
        IsAuthenticated = false;
        AuthStatus = "Not authenticated";
        Messages.Clear();
        history.Clear();
    }
}
