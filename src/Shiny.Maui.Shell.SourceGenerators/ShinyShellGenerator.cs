using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Shiny.Maui.Shell.SourceGenerators;


[Generator(LanguageNames.CSharp)]
public class ShinyShellGenerator : IIncrementalGenerator
{
    static readonly DiagnosticDescriptor InvalidRouteIdentifier = new(
        "SHINY001",
        "Invalid route name",
        "The route '{0}' does not produce a valid C# identifier '{1}'. Route must contain at least one letter and cannot start with a digit after conversion.",
        "Shiny.Shell",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find classes with ShellMapAttribute
        var shellMapClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetShellMapClass(ctx))
            .Where(static m => m is not null)
            .Collect();

        var options = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_GenerateRouteConstants", out var routeValue);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_GenerateNavExtensions", out var navValue);
                // empty or missing is considered true; only explicit "false" disables
                return (
                    GenerateRouteConstants: !string.Equals(routeValue, "false", StringComparison.OrdinalIgnoreCase),
                    GenerateNavExtensions: !string.Equals(navValue, "false", StringComparison.OrdinalIgnoreCase)
                );
            });

        var combined = shellMapClasses.Combine(options);

        context.RegisterSourceOutput(combined, (spc, data) => GenerateCode(spc, data.Left, data.Right));
    }

    static ShellMapInfo? GetShellMapClass(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        
        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
                if (symbolInfo.Symbol is IMethodSymbol attributeSymbol)
                {
                    var attributeClass = attributeSymbol.ContainingType;
                    if (attributeClass.Name == "ShellMapAttribute" && attributeClass.IsGenericType)
                    {
                        var pageType = attributeClass.TypeArguments[0];
                        var route = GetRouteFromAttribute(attribute);
                        var registerRoute = GetRegisterRouteFromAttribute(attribute);
                        var properties = GetShellProperties(classDeclaration, context.SemanticModel);
                        
                        var viewModelSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
                        var generatedName = route ?? pageType.Name.Replace("Page", "");
                        return new ShellMapInfo(
                            classDeclaration.Identifier.ValueText,
                            viewModelSymbol?.ToDisplayString() ?? classDeclaration.Identifier.ValueText,
                            pageType.Name,
                            pageType.ToDisplayString(),
                            route ?? pageType.Name,
                            generatedName,
                            registerRoute,
                            properties,
                            attribute.GetLocation()
                        );
                    }
                }
            }
        }
        
        return null;
    }

    static string? GetRouteFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            // Look for the route parameter specifically
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                // Check if it's a named argument for "route"
                if (arg.NameColon?.Name.Identifier.ValueText == "route")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal)
                    {
                        return literal.Token.ValueText;
                    }
                }
                // If it's the first positional argument and not a named argument for registerRoute
                else if (arg == attribute.ArgumentList.Arguments[0] && 
                         arg.NameColon?.Name.Identifier.ValueText != "registerRoute")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal &&
                        literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                    {
                        return literal.Token.ValueText;
                    }
                }
            }
        }
        return null;
    }

    static bool GetRegisterRouteFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            var arguments = attribute.ArgumentList.Arguments;
            
            // Look for the registerRoute parameter specifically
            foreach (var arg in arguments)
            {
                // Check if it's a named argument for "registerRoute"
                if (arg.NameColon?.Name.Identifier.ValueText == "registerRoute")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal)
                    {
                        if (literal.Token.IsKind(SyntaxKind.FalseKeyword))
                            return false;
                        if (literal.Token.IsKind(SyntaxKind.TrueKeyword))
                            return true;
                    }
                }
            }
            
            // Check positional arguments
            for (int i = 0; i < arguments.Count; i++)
            {
                var arg = arguments[i];
                
                // If it's the second positional argument (index 1) and not a named argument
                if (i == 1 && arg.NameColon == null)
                {
                    if (arg.Expression is LiteralExpressionSyntax literal)
                    {
                        if (literal.Token.IsKind(SyntaxKind.FalseKeyword))
                            return false;
                        if (literal.Token.IsKind(SyntaxKind.TrueKeyword))
                            return true;
                    }
                }
                // Handle case where registerRoute is the first argument (when route is omitted)
                else if (i == 0 && 
                         arg.NameColon == null &&
                         arg.Expression is LiteralExpressionSyntax literal &&
                         (literal.Token.IsKind(SyntaxKind.TrueKeyword) || literal.Token.IsKind(SyntaxKind.FalseKeyword)))
                {
                    if (literal.Token.IsKind(SyntaxKind.FalseKeyword))
                        return false;
                    if (literal.Token.IsKind(SyntaxKind.TrueKeyword))
                        return true;
                }
            }
        }
        // Default value is true according to the attribute definition
        return true;
    }

    static ImmutableArray<ShellPropertyInfo> GetShellProperties(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        var properties = ImmutableArray.CreateBuilder<ShellPropertyInfo>();
        
        foreach (var member in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            foreach (var attributeList in member.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(attribute);
                    if (symbolInfo.Symbol is IMethodSymbol attributeSymbol &&
                        attributeSymbol.ContainingType.Name == "ShellPropertyAttribute")
                    {
                        var isRequired = GetIsRequiredFromAttribute(attribute);
                        var propertySymbol = semanticModel.GetDeclaredSymbol(member) as IPropertySymbol;
                        
                        if (propertySymbol != null)
                        {
                            // Check if property has public get/set
                            var hasPublicGetter = propertySymbol.GetMethod?.DeclaredAccessibility == Accessibility.Public;
                            var hasPublicSetter = propertySymbol.SetMethod?.DeclaredAccessibility == Accessibility.Public;
                            
                            if (!hasPublicGetter || !hasPublicSetter)
                            {
                                // This would ideally be a diagnostic error, but for now we'll skip
                                continue;
                            }
                            else
                            {
                                properties.Add(new ShellPropertyInfo(
                                    member.Identifier.ValueText,
                                    propertySymbol.Type.ToDisplayString(),
                                    isRequired
                                ));
                            }
                        }
                    }
                }
            }
        }
        
        return properties.ToImmutable();
    }

    static bool GetIsRequiredFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            var firstArg = attribute.ArgumentList.Arguments[0];
            if (firstArg.Expression is LiteralExpressionSyntax literal &&
                literal.Token.IsKind(SyntaxKind.TrueKeyword))
            {
                return true;
            }
        }
        return false;
    }

    static void GenerateCode(SourceProductionContext context, ImmutableArray<ShellMapInfo?> classes, (bool GenerateRouteConstants, bool GenerateNavExtensions) options)
    {
        var validClasses = classes.Where(c => c != null).Cast<ShellMapInfo>().ToImmutableArray();

        // Validate generated names are valid C# identifiers
        var checkedClasses = ImmutableArray.CreateBuilder<ShellMapInfo>();
        foreach (var cls in validClasses)
        {
            if (!SyntaxFacts.IsValidIdentifier(cls.GeneratedName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteIdentifier,
                    cls.AttributeLocation,
                    cls.Route,
                    cls.GeneratedName
                ));
            }
            else
            {
                checkedClasses.Add(cls);
            }
        }
        var filtered = checkedClasses.ToImmutable();

        // Always generate AddGeneratedMaps so user can use it immediately
        GenerateNavigationBuilderExtensions(context, filtered);

        if (filtered.IsEmpty)
            return;

        // Generate Routes class only if enabled
        if (options.GenerateRouteConstants)
            GenerateRoutesClass(context, filtered);
        
        // Generate NavigationExtensions class only if enabled
        if (options.GenerateNavExtensions)
            GenerateNavigationExtensions(context, filtered);
    }

    static void GenerateRoutesClass(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("public static class Routes");
        sb.AppendLine("{");
        
        foreach (var cls in classes)
        {
            var constantName = cls.GeneratedName;
            sb.AppendLine($"    public const string {constantName} = \"{cls.Route}\";");
        }
        
        sb.AppendLine("}");
        
        context.AddSource("Routes.g.cs", sb.ToString());
    }

    static void GenerateNavigationExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public static class NavigationExtensions");
        sb.AppendLine("{");
        
        foreach (var cls in classes)
        {
            var methodName = $"NavigateTo{cls.GeneratedName}";
            var requiredParams = cls.Properties.Where(p => p.IsRequired).ToList();
            var optionalParams = cls.Properties.Where(p => !p.IsRequired).ToList();
            
            sb.Append($"    public static global::System.Threading.Tasks.Task {methodName}(this global::Shiny.INavigator navigator");

            // Add required parameters first
            foreach (var prop in requiredParams)
            {
                sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)}");
            }

            // Add optional parameters last
            foreach (var prop in optionalParams)
            {
                var defaultValue = GetDefaultValue(prop.TypeName);
                sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
            }

            sb.Append(", bool relativeNavigation = true");

            // If no properties, add the params argument
            if (!cls.Properties.Any())
            {
                sb.Append(", params global::System.Collections.Generic.IEnumerable<(string Key, object Value)> args");
            }

            sb.AppendLine(")");
            sb.AppendLine("    {");

            if (cls.Properties.Any())
            {
                sb.Append($"        return navigator.NavigateTo<{cls.ViewModelFullName}>(x => ");
                sb.Append("{ ");

                var assignments = cls.Properties.Select(p => $"x.{p.Name} = {ToCamelCase(p.Name)}");
                sb.Append(string.Join("; ", assignments));
                sb.Append(";");

                sb.AppendLine($" }}, relativeNavigation);");
            }
            else
            {
                sb.AppendLine($"        return navigator.NavigateTo<{cls.ViewModelFullName}>(configure: null, relativeNavigation: relativeNavigation, args: args);");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("}");
        
        context.AddSource("NavigationExtensions.g.cs", sb.ToString());
    }

    static void GenerateNavigationBuilderExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("public static class NavigationBuilderExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Shiny.ShinyAppBuilder AddGeneratedMaps(this global::Shiny.ShinyAppBuilder builder)");
        sb.AppendLine("    {");
        
        foreach (var cls in classes)
        {
            if (cls.RegisterRoute)
            {
                sb.AppendLine($"        builder.Add<{cls.PageTypeFullName}, {cls.ViewModelFullName}>(\"{cls.Route}\");");
            }
            else
            {
                sb.AppendLine($"        builder.Add<{cls.PageTypeFullName}, {cls.ViewModelFullName}>(\"{cls.Route}\", registerRoute: false);");
            }
        }
        
        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        context.AddSource("NavigationBuilderExtensions.g.cs", sb.ToString());
    }

    static string ToCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text) || char.IsLower(text[0]))
            return text;
        return char.ToLower(text[0]) + text.Substring(1);
    }

    static string GetDefaultValue(string typeName)
    {
        return typeName.EndsWith("?") || typeName == "string" ? "null" : "default";
    }
}

record ShellMapInfo(
    string ViewModelName,
    string ViewModelFullName,
    string PageTypeName,
    string PageTypeFullName,
    string Route,
    string GeneratedName,
    bool RegisterRoute,
    ImmutableArray<ShellPropertyInfo> Properties,
    Location? AttributeLocation
);

record ShellPropertyInfo(
    string Name,
    string TypeName,
    bool IsRequired
);