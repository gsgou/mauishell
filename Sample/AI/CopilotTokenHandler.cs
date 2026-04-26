using System.Net.Http.Headers;

namespace Sample.AI;

public class CopilotTokenHandler(GitHubCopilotAuthService authService) : DelegatingHandler
{
    public CopilotTokenHandler(GitHubCopilotAuthService authService, HttpMessageHandler inner)
        : this(authService) => InnerHandler = inner;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await authService.GetCopilotTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Required header for the Copilot API
        if (!request.Headers.Contains("Copilot-Integration-Id"))
            request.Headers.TryAddWithoutValidation("Copilot-Integration-Id", "vscode-chat");

        return await base.SendAsync(request, cancellationToken);
    }
}
