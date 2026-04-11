using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shiny.Maui.Shell.SourceGenerators;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

public class ShinyShellGeneratorTests
{
    const string StubTypes = @"
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Shiny;

namespace Shiny
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ShellMapAttribute<TPage> : Attribute
    {
        public ShellMapAttribute(string route = null, bool registerRoute = true) { }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ShellPropertyAttribute : Attribute
    {
        public ShellPropertyAttribute(bool required = true) { }
    }

    public interface INavigator
    {
        Task NavigateTo<TViewModel>(Action<TViewModel> configure = null, bool relativeNavigation = true);
        Task NavigateTo<TViewModel>(Action<TViewModel> configure = null, bool relativeNavigation = true, params IEnumerable<(string Key, object Value)> args);
    }

    public sealed class ShinyAppBuilder
    {
        public ShinyAppBuilder Add<TPage, TViewModel>(string route = null, bool registerRoute = true) => this;
    }
}

namespace Microsoft.Maui.Controls
{
    public class Page { }
}
";

    #region Route Constant Generation

    [Fact]
    public void RouteConstants_DefaultRoute_UsesPageNameWithoutPageSuffix()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var routesSource = GetGeneratedSource(result, "Routes.g.cs");

        routesSource.ShouldContain("public const string Home = \"HomePage\";");
    }

    [Fact]
    public void RouteConstants_ExplicitRoute_UsesRouteAsConstantName()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var routesSource = GetGeneratedSource(result, "Routes.g.cs");

        routesSource.ShouldContain("public const string Dashboard = \"Dashboard\";");
        routesSource.ShouldNotContain("Home");
    }

    [Fact]
    public void RouteConstants_NamedRouteParameter_UsesRouteAsConstantName()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class SettingsPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<SettingsPage>(route: ""Preferences"")]
    public class SettingsViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var routesSource = GetGeneratedSource(result, "Routes.g.cs");

        routesSource.ShouldContain("public const string Preferences = \"Preferences\";");
    }

    #endregion

    #region Disable Route Constants

    [Fact]
    public void RouteConstants_DisabledViaProperty_NotGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateRouteConstants", "false"));

        GetGeneratedSourceOrDefault(result, "Routes.g.cs").ShouldBeNull();
        GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs").ShouldNotBeNull();
    }

    [Fact]
    public void RouteConstants_EmptyProperty_StillGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateRouteConstants", ""));

        GetGeneratedSource(result, "Routes.g.cs").ShouldNotBeNull();
    }

    [Fact]
    public void RouteConstants_MissingProperty_StillGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        GetGeneratedSource(result, "Routes.g.cs").ShouldNotBeNull();
    }

    #endregion

    #region Disable Nav Extensions

    [Fact]
    public void NavExtensions_DisabledViaProperty_NotGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateNavExtensions", "false"));

        GetGeneratedSourceOrDefault(result, "NavigationExtensions.g.cs").ShouldBeNull();
        GetGeneratedSource(result, "Routes.g.cs").ShouldNotBeNull();
    }

    [Fact]
    public void NavExtensions_EmptyProperty_StillGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateNavExtensions", ""));

        GetGeneratedSource(result, "NavigationExtensions.g.cs").ShouldNotBeNull();
    }

    [Fact]
    public void BothDisabled_OnlyGeneratesBuilderExtensions()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source,
            ("ShinyMauiShell_GenerateRouteConstants", "false"),
            ("ShinyMauiShell_GenerateNavExtensions", "false"));

        GetGeneratedSourceOrDefault(result, "Routes.g.cs").ShouldBeNull();
        GetGeneratedSourceOrDefault(result, "NavigationExtensions.g.cs").ShouldBeNull();
        GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs").ShouldNotBeNull();
    }

    #endregion

    #region Navigation Extension Method Naming

    [Fact]
    public void NavExtensions_DefaultRoute_MethodUsesPageName()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var navSource = GetGeneratedSource(result, "NavigationExtensions.g.cs");

        navSource.ShouldContain("NavigateToHome");
    }

    [Fact]
    public void NavExtensions_ExplicitRoute_MethodUsesRouteName()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var navSource = GetGeneratedSource(result, "NavigationExtensions.g.cs");

        navSource.ShouldContain("NavigateToDashboard");
        navSource.ShouldNotContain("NavigateToHome");
    }

    #endregion

    #region Builder Extensions Use String Literals

    [Fact]
    public void BuilderExtensions_UsesStringLiterals_NotRouteConstants()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var builderSource = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        builderSource.ShouldContain("\"Dashboard\"");
        builderSource.ShouldNotContain("Routes.");
    }

    [Fact]
    public void BuilderExtensions_RegisterRouteFalse_PassesParameter()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class MainPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<MainPage>(registerRoute: false)]
    public class MainViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var builderSource = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        builderSource.ShouldContain("registerRoute: false");
    }

    #endregion

    #region Empty Maps Still Generates Builder

    [Fact]
    public void NoShellMaps_StillGeneratesBuilderExtensions()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }
}";
        var result = RunGenerator(source);
        var builderSource = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        builderSource.ShouldContain("AddGeneratedMaps");
        builderSource.ShouldContain("return builder;");
    }

    #endregion

    #region Invalid Route Name Diagnostic

    [Fact]
    public void InvalidRoute_StartsWithDigit_ReportsError()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""123invalid"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY001");
    }

    [Fact]
    public void InvalidRoute_ContainsHyphen_ReportsError()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class MyPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<MyPage>(""my-route"")]
    public class MyViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY001");
    }

    [Fact]
    public void InvalidRoute_ExcludedFromGeneration()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class GoodPage : Microsoft.Maui.Controls.Page { }
    public class BadPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<GoodPage>(""Valid"")]
    public class GoodViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    [ShellMap<BadPage>(""123bad"")]
    public class BadViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var routesSource = GetGeneratedSource(result, "Routes.g.cs");

        routesSource.ShouldContain("Valid");
        routesSource.ShouldNotContain("123bad");
        routesSource.ShouldNotContain("Bad");
    }

    [Fact]
    public void ValidRoute_NoDiagnostic()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY001");
    }

    #endregion

    #region Helpers

    static GeneratorRunResult RunGenerator(string source, params (string Key, string Value)[] buildProperties)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ShinyShellGenerator();

        var dict = buildProperties.ToDictionary(
            x => "build_property." + x.Key,
            x => x.Value,
            StringComparer.OrdinalIgnoreCase);

        var provider = new MockAnalyzerConfigOptionsProvider(dict);
        var driver = CSharpGeneratorDriver.Create(generator).WithUpdatedAnalyzerConfigOptions(provider);
        var ran = driver.RunGenerators(compilation);

        return ran.GetRunResult().Results.First();
    }

    static string GetGeneratedSource(GeneratorRunResult result, string hintName)
    {
        var source = GetGeneratedSourceOrDefault(result, hintName);
        source.ShouldNotBeNull($"Expected generated source '{hintName}' was not found. Available: {string.Join(", ", result.GeneratedSources.Select(s => s.HintName))}");
        return source;
    }

    static string? GetGeneratedSourceOrDefault(GeneratorRunResult result, string hintName)
    {
        return result.GeneratedSources
            .FirstOrDefault(s => s.HintName == hintName)
            .SourceText?
            .ToString();
    }

    #endregion
}
