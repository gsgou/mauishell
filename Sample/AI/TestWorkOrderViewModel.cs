using Shiny;

namespace Sample.AI;

public enum WorkOrderPriority
{
    Low,
    Medium,
    High,
    Urgent
}

[ShellMap<TestWorkOrderPage>(description: "Use when the user reports something broken, malfunctioning, needing repair, maintenance, or service. Examples: equipment failures, outages, leaks, HVAC issues, or any physical problem that needs to be fixed.")]
public partial class TestWorkOrderViewModel(INavigator navigator) : ObservableObject
{
    [ShellProperty("Summarize what is broken or what needs to be done based on what the user said", required: true)]
    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ShellProperty("Infer the urgency from the user's tone and words. Must be one of: Low, Medium, High, Urgent", required: true)]
    [ObservableProperty]
    public partial WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;

    [ShellProperty("The physical location if the user mentioned one, otherwise leave empty", required: false)]
    [ObservableProperty]
    public partial string Location { get; set; } = string.Empty;

    [ShellProperty("The name of the person if they identified themselves, otherwise leave empty", required: false)]
    [ObservableProperty]
    public partial string RequestedBy { get; set; } = string.Empty;

    [ObservableProperty]
    bool isSubmitted;

    [RelayCommand]
    void Submit() => IsSubmitted = true;

    [RelayCommand]
    Task GoBack() => navigator.GoBack();
}
