using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.ObjectModel;
using Microsoft.Extensions.AI;
using OpenAI;
using Shiny;
using Shiny.Maui.Controls.Chat;

using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatMessage = Shiny.Maui.Controls.Chat.ChatMessage;

namespace Sample.AI;

[ShellMap<ChatPage>]
public partial class ChatViewModel(
    INavigator navigator,
    GitHubCopilotAuthService authService
) : ObservableObject, IPageLifecycleAware
{
    IChatClient? chatClient;
    CancellationTokenSource? cts;
    readonly List<AIChatMessage> history = [];

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    bool isBusy;

    [ObservableProperty]
    string authStatus = "Not authenticated";

    [ObservableProperty]
    string? userCode;

    [ObservableProperty]
    bool isAuthenticated;

    public bool IsNotBusy => !IsBusy;

    public void OnAppearing() { }
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
        history.Add(new AIChatMessage(ChatRole.System, "You are a helpful assistant integrated in a .NET MAUI app. You can help navigate the app using the provided tools."));

        Messages.Add(new ChatMessage
        {
            Text = "Yes master, how may I serve thee?",
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

        history.Add(new AIChatMessage(ChatRole.User, text));

        try
        {
            IsBusy = true;
            cts = new CancellationTokenSource();

            var options = new ChatOptions
            {
                Tools =
                [
                    AIFunctionFactory.Create(navigator.GetAiToolApplicableGeneratedRoutes),
                    AIFunctionFactory.Create(navigator.NavigateToRoute)
                ]
            };

            var response = await chatClient.GetResponseAsync(history, options, cts.Token);
            var assistantText = response.Text ?? "(no response)";

            history.Add(new AIChatMessage(ChatRole.Assistant, assistantText));
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
