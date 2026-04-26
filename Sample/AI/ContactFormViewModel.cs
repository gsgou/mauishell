using Shiny;

namespace Sample.AI;

[ShellMap<ContactFormPage>(description: "Use when the user wants to get in touch, reach out, ask a question, request information, schedule a meeting, provide feedback, or have a conversation with someone. Any intent to communicate or make contact.")]
public partial class ContactFormViewModel(INavigator navigator) : ObservableObject, IQueryAttributable
{
    [ShellProperty("The person's name if they provided it, otherwise leave empty", required: false)]
    public string Name { get; set; } = string.Empty;

    [ShellProperty("The email address if the user provided one, otherwise leave empty", required: false)]
    public string Email { get; set; } = string.Empty;

    [ShellProperty("A phone number if the user provided one, otherwise leave empty", required: false)]
    public string Phone { get; set; } = string.Empty;

    [ShellProperty("Infer the topic or reason for contact from what the user said", required: false)]
    public string Subject { get; set; } = string.Empty;

    [ShellProperty("Any additional context or details the user mentioned that don't fit other fields", required: false)]
    public string Message { get; set; } = string.Empty;

    [ObservableProperty]
    bool isSubmitted;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Name", out var name))
            Name = name?.ToString() ?? string.Empty;

        if (query.TryGetValue("Email", out var email))
            Email = email?.ToString() ?? string.Empty;

        if (query.TryGetValue("Phone", out var phone))
            Phone = phone?.ToString() ?? string.Empty;

        if (query.TryGetValue("Subject", out var subject))
            Subject = subject?.ToString() ?? string.Empty;

        if (query.TryGetValue("Message", out var msg))
            Message = msg?.ToString() ?? string.Empty;

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Email));
        OnPropertyChanged(nameof(Phone));
        OnPropertyChanged(nameof(Subject));
        OnPropertyChanged(nameof(Message));
    }

    [RelayCommand]
    void Submit() => IsSubmitted = true;

    [RelayCommand]
    Task GoBack() => navigator.GoBack();
}
