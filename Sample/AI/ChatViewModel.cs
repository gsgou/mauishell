using Microsoft.Extensions.AI;
using Shiny;

namespace Sample.AI;

[ShellMap<ChatPage>]
public partial class ChatViewModel(INavigator navigator) : ObservableObject, IPageLifecycleAware
{
    public void OnAppearing()
    {
        IChatClient client = null!; //new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.1");

        client = ChatClientBuilderChatClientExtensions
            .AsBuilder(client)
            .UseFunctionInvocation()
            .Build();
        
        string GetCurrentWeather() => Random.Shared.NextDouble() > 0.5 ? "It's sunny" : "It's raining";

        
        //AITool
        ChatOptions options = new() { Tools = [
            AIFunctionFactory.Create(navigator.GetGeneratedRouteInfo),
            AIFunctionFactory.Create(navigator.NavigateTo),
            AIFunctionFactory.Create(navigator.NavigateToDetail)
        ]};
    }

    public void OnDisappearing()
    {
    }
}