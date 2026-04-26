using Shiny;

namespace Sample.AI;

[ShellMap<TestWorkOrderPage>(description: "Use when the user reports something broken, malfunctioning, needing repair, maintenance, or service. Examples: equipment failures, outages, leaks, HVAC issues, or any physical problem that needs to be fixed.")]
public partial class TestWorkOrderViewModel(INavigator navigator) : ObservableObject, IQueryAttributable
{
    [ShellProperty("Summarize what is broken or what needs to be done based on what the user said", required: true)]
    public string Description { get; set; } = string.Empty;

    [ShellProperty("Infer the urgency from the user's tone and words. Must be one of: Low, Medium, High, Urgent", required: true)]
    public string Priority { get; set; } = "Medium";

    [ShellProperty("The physical location if the user mentioned one, otherwise leave empty", required: false)]
    public string Location { get; set; } = string.Empty;

    [ShellProperty("The name of the person if they identified themselves, otherwise leave empty", required: false)]
    public string RequestedBy { get; set; } = string.Empty;

    [ObservableProperty]
    bool isSubmitted;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Description", out var desc))
            Description = desc?.ToString() ?? string.Empty;

        if (query.TryGetValue("Priority", out var priority))
            Priority = priority?.ToString() ?? "Medium";

        if (query.TryGetValue("Location", out var loc))
            Location = loc?.ToString() ?? string.Empty;

        if (query.TryGetValue("RequestedBy", out var req))
            RequestedBy = req?.ToString() ?? string.Empty;

        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(Location));
        OnPropertyChanged(nameof(RequestedBy));
    }

    [RelayCommand]
    void Submit() => IsSubmitted = true;

    [RelayCommand]
    Task GoBack() => navigator.GoBack();
}
