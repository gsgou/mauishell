# Shiny MAUI Shell

[![NuGet](https://img.shields.io/nuget/v/Shiny.Maui.Shell?style=for-the-badge)](https://www.nuget.org/packages/Shiny.Maui.Shell)

Make .NET MAUI Shell shinier with ViewModel lifecycle management, navigation services, and source generation to remove boilerplate, reduce errors, and make your app testable.

Inspired by [Prism Library](https://prismlibrary.com) by Dan Siegel and Brian Lagunas.

[Full Documentation](https://shinylib.net/maui)

---

## Features

### 🧭 Navigation — `INavigator`

| Capability | Description |
|:-----------|:------------|
| Route-based | `NavigateTo("Detail", args: [("Id", "123")])` |
| ViewModel-based | `NavigateTo<DetailViewModel>(vm => vm.Id = "123")` |
| Source-generated | `NavigateToDetail("123")` — zero guesswork |
| GoBack | Single page, multi-page `GoBack(3)`, or `PopToRoot()` |
| Root navigation | `NavigateTo<DashboardViewModel>(relativeNavigation: false)` — reset the stack |
| Navigation builder | Fluent multi-segment: `CreateBuilder().AddDetail(42).AddModal().Navigate()` |
| Shell switching | `SwitchShell(new MainShell())` or `SwitchShell<TShell>()` via DI |

### 💬 Dialogs — `IDialogs`

| Method | Returns |
|:-------|:--------|
| `Alert(title, message)` | `Task` |
| `Confirm(title, message)` | `Task<bool>` |
| `Prompt(title, message)` | `Task<string?>` |
| `ActionSheet(title, cancel, destructive, ...buttons)` | `Task<string>` |

> Thread-safe — dispatches to UI thread automatically. Inject separately from `INavigator` for clean separation of concerns.

### 📡 Navigation Events

| Event | Fires | Key Properties |
|:------|:------|:---------------|
| `Navigating` | Before navigation | `FromUri` · `FromViewModel` · `ToUri` · `NavigationType` · `Parameters` |
| `Navigated` | After page resolves | `ToUri` · `ToViewModel` · `NavigationType` · `Parameters` |

`NavigationType`: `Push` · `SetRoot` · `GoBack` · `PopToRoot` · `SwitchShell`

### ♻️ ViewModel Lifecycle

| Interface | Method | Purpose |
|:----------|:-------|:--------|
| `IPageLifecycleAware` | `OnAppearing()` / `OnDisappearing()` | Page visibility hooks |
| `INavigationConfirmation` | `Task<bool> CanNavigate()` | Guard navigation (unsaved changes, etc.) |
| `INavigationAware` | `OnNavigatingFrom(params)` | Mutate parameters before leaving |
| `IQueryAttributable` | `ApplyQueryAttributes(params)` | Receive navigation parameters |
| `IDisposable` | `Dispose()` | Cleanup when page leaves the stack |

### ⚡ Source Generation

| Generated File | What It Does |
|:----------------|:------------|
| `Routes.g.cs` | Static route constants — `Routes.Detail` |
| `NavigationExtensions.g.cs` | Typed methods — `NavigateToDetail(id, page)` |
| `NavigationBuilderNavExtensions.g.cs` | Typed builder methods — `AddDetail(id, page)` |
| `NavigationBuilderExtensions.g.cs` | One-line DI — `AddGeneratedMaps()` |

> Invalid route names produce **SHINY001** compiler errors. Disable individual outputs via MSBuild properties.

### ✅ Zero Ceremony

- One base class change — `AppShell : ShinyShell` — for deterministic BindingContext assignment
- Page–ViewModel mapping with **automatic BindingContext** assignment
- Drop-in `[ShellMap]` attribute replaces manual route registration

---

## Getting Started

### 1. Install

```bash
dotnet add package Shiny.Maui.Shell
```

### 2. Configure MauiProgram.cs

**With source generation (recommended):**
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x.AddGeneratedMaps());
```

**Manual registration:**
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x
        .Add<MainPage, MainViewModel>(registerRoute: false) // pages in AppShell.xaml
        .Add<DetailPage, DetailViewModel>("Detail")
        .Add<SettingsPage, SettingsViewModel>("Settings")
    );
```

### 3. Set up AppShell

Your `AppShell` must inherit from `ShinyShell` instead of `Shell`:

**AppShell.xaml:**
```xml
<shiny:ShinyShell
    x:Class="MyApp.AppShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Shell"
    xmlns:local="clr-namespace:MyApp"
    Title="MyApp">

    <ShellContent
        Title="Home"
        ContentTemplate="{DataTemplate local:MainPage}"
        Route="MainPage" />

</shiny:ShinyShell>
```

**AppShell.xaml.cs:**
```csharp
using Shiny;

namespace MyApp;

public partial class AppShell : ShinyShell
{
    public AppShell()
    {
        InitializeComponent();
    }
}
```

> [!NOTE]
> Pages defined in AppShell.xaml should use `registerRoute: false`.

### 4. Navigate

Inject `INavigator` into your ViewModels:

```csharp
public class MyViewModel(INavigator navigator)
{
    // Route-based navigation with args
    await navigator.NavigateTo("Detail", args: [("ItemId", "123")]);

    // ViewModel-based navigation with strongly-typed configuration
    await navigator.NavigateTo<DetailViewModel>(vm => vm.ItemId = "123");

    // Source-generated strongly-typed method (preferred)
    await navigator.NavigateToDetail("123");

    // Root navigation — resets the stack
    await navigator.NavigateTo<DashboardViewModel>(relativeNavigation: false);

    // Go back with result
    await navigator.GoBack(("Result", selectedItem));

    // Go back multiple pages
    await navigator.GoBack(2);

    // Pop to root
    await navigator.PopToRoot();

    // Switch to a different Shell instance
    await navigator.SwitchShell(new MainAppShell());

    // Switch to a Shell resolved from DI
    await navigator.SwitchShell<MainAppShell>();

    // Fluent multi-segment navigation builder
    await navigator
        .CreateBuilder()
        .AddDetail(id: 42)
        .AddModal()
        .Navigate();

    // Pop back 2 pages, then push
    await navigator
        .CreateBuilder()
        .PopBack(2)
        .AddHome()
        .Navigate();

    // Navigate from root with builder
    await navigator
        .CreateBuilder(fromRoot: true)
        .AddDashboard()
        .AddDetail(id: 1)
        .Navigate();
}
```

> [!IMPORTANT]
> Root navigation (`relativeNavigation: false` or `CreateBuilder(fromRoot: true)`) uses the `//` URI prefix, which requires the target route to be declared in your `AppShell.xaml`. Routes registered only via `Routing.RegisterRoute` or `[ShellMap]` cannot be navigated to from root. Add the page as a `ShellContent` in your Shell XAML and use `registerRoute: false` in `[ShellMap]`.

> [!NOTE]
> If you're setting arguments on the ViewModel navigation, you should make them observable if they are bound on the Page.

### 5. Dialogs

Inject `IDialogs` for user-facing dialogs:

```csharp
public class MyViewModel(IDialogs dialogs)
{
    // Alert
    await dialogs.Alert("Error", "Something went wrong");

    // Confirm
    if (await dialogs.Confirm("Delete?", "Are you sure?"))
    {
        // delete
    }

    // Prompt for text input
    var name = await dialogs.Prompt("Name", "Enter your name", placeholder: "John Doe");
    if (name != null)
    {
        // user entered a value
    }

    // Action sheet
    var choice = await dialogs.ActionSheet("Options", "Cancel", "Delete", "Edit", "Share");
}
```

---

## Navigation Events

Subscribe to `Navigating` and `Navigated` on `INavigator` for cross-cutting concerns like logging or analytics:

```csharp
public class NavigationLogger(
    ILogger<NavigationLogger> logger,
    INavigator navigator
) : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        navigator.Navigating += (_, args) =>
            logger.LogInformation("Navigating from '{From}' to '{To}' ({Type})",
                args.FromUri, args.ToUri, args.NavigationType);

        navigator.Navigated += (_, args) =>
            logger.LogInformation("Navigated to '{To}' - ViewModel: {VM} ({Type})",
                args.ToUri, args.ToViewModel?.GetType().Name, args.NavigationType);
    }
}

// Register in MauiProgram.cs
builder.Services.AddSingleton<IMauiInitializeService, NavigationLogger>();
```

---

## ViewModel Lifecycle

Implement these interfaces on your ViewModels as needed. Works just like [Prism Library](https://prismlibrary.com).

```csharp
[ShellMap<DetailPage>("Detail")]
public partial class DetailViewModel(INavigator navigator, IDialogs dialogs) : ObservableObject,
    IQueryAttributable,
    IPageLifecycleAware,
    INavigationConfirmation,
    IDisposable
{
    [ShellProperty]
    [ObservableProperty]
    string itemId;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(ItemId), out var id))
            ItemId = id?.ToString();
    }

    public void OnAppearing() { /* load data */ }
    public void OnDisappearing() { /* pause */ }

    public async Task<bool> CanNavigate()
    {
        if (!hasUnsavedChanges) return true;
        return await dialogs.Confirm("Unsaved Changes", "Discard changes?");
    }

    public void Dispose() { /* cleanup */ }
}
```

---

## Source Generation

Decorate your ViewModels with `[ShellMap]` and `[ShellProperty]` to eliminate boilerplate:

**Input:**
```csharp
[ShellMap<DetailPage>("Detail")]
public partial class DetailViewModel : ObservableObject
{
    [ShellProperty]
    public string ItemId { get; set; }

    [ShellProperty(required: false)]
    public int Page { get; set; }
}
```

**Generated output:**

```csharp
// Routes.g.cs — constant name matches the route parameter
public static class Routes
{
    public const string Detail = "Detail";
}

// NavigationExtensions.g.cs — typed INavigator methods
public static class NavigationExtensions
{
    public static Task NavigateToDetail(this INavigator navigator, string itemId,
        int page = default, bool relativeNavigation = true)
    {
        return navigator.NavigateTo<DetailViewModel>(x =>
        {
            x.ItemId = itemId;
            x.Page = page;
        }, relativeNavigation);
    }
}

// NavigationBuilderNavExtensions.g.cs — typed INavigationBuilder methods
public static class NavigationBuilderNavExtensions
{
    public static INavigationBuilder AddDetail(this INavigationBuilder builder,
        string itemId, int page = default)
    {
        return builder.Add<DetailViewModel>(x => { x.ItemId = itemId; x.Page = page; });
    }
}

// NavigationBuilderExtensions.g.cs — uses string literals (not Routes.*)
public static class NavigationBuilderExtensions
{
    public static ShinyAppBuilder AddGeneratedMaps(this ShinyAppBuilder builder)
    {
        builder.Add<DetailPage, DetailViewModel>("Detail");
        return builder;
    }
}
```

Then use it:
```csharp
// MauiProgram.cs - one line to register everything
builder.UseShinyShell(x => x.AddGeneratedMaps());

// Navigate with generated extension methods - no guesswork
await navigator.NavigateToDetail("123", page: 2);

// Fluent builder with generated extensions
await navigator.CreateBuilder().AddDetail("123", page: 2).Navigate();
```

### Route Naming

The `route` parameter in `[ShellMap]` drives the generated constant and method names. It must be a valid C# identifier — invalid names produce a **SHINY001** compiler error.

```csharp
// Route drives the constant and method name
[ShellMap<HomePage>("Dashboard")]
// → Routes.Dashboard = "Dashboard"
// → NavigateToDashboard(...)

// No route — falls back to page type name without "Page" suffix
[ShellMap<HomePage>]
// → Routes.Home = "HomePage"
// → NavigateToHome(...)
```

### Configuring Source Generation

Disable individual generated files via MSBuild properties:

```xml
<PropertyGroup>
    <!-- Disable Routes.g.cs -->
    <ShinyMauiShell_GenerateRouteConstants>false</ShinyMauiShell_GenerateRouteConstants>

    <!-- Disable NavigationExtensions.g.cs -->
    <ShinyMauiShell_GenerateNavExtensions>false</ShinyMauiShell_GenerateNavExtensions>
</PropertyGroup>
```

`NavigationBuilderExtensions.g.cs` (`AddGeneratedMaps()`) is always generated — even when no `[ShellMap]` attributes exist yet — so you can wire up `MauiProgram.cs` immediately.
