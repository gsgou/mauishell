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

    static readonly DiagnosticDescriptor NavExtensionsDisabledWithMaps = new(
        "SHINY002",
        "Navigation extensions disabled but ShellMap attributes detected",
        "ShinyMauiShell_GenerateNavExtensions is set to false but {0} ShellMap attribute(s) were detected. AddGeneratedMaps will not be generated.",
        "Shiny.Shell",
        DiagnosticSeverity.Warning,
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
                        var description = GetDescriptionFromAttribute(attribute);
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
                            description,
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
                // If it's the first positional argument (no named colon)
                else if (arg == attribute.ArgumentList.Arguments[0] && arg.NameColon == null)
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

    static string GetDescriptionFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                if (arg.NameColon?.Name.Identifier.ValueText == "description")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal &&
                        literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                        return literal.Token.ValueText;
                    return null;
                }
            }

            // Positional: description is 3rd param (index 2) on ShellMapAttribute(route, registerRoute, description)
            int positionalIndex = 0;
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                if (arg.NameColon != null)
                    continue;

                if (positionalIndex == 2 &&
                    arg.Expression is LiteralExpressionSyntax literal &&
                    literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                    return literal.Token.ValueText;

                positionalIndex++;
            }
        }
        return null;
    }

    static string GetPropertyDescriptionFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                if (arg.NameColon?.Name.Identifier.ValueText == "description")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal &&
                        literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                        return literal.Token.ValueText;
                    return null;
                }
            }

            // Positional: description is 1st param on ShellPropertyAttribute
            var firstArg = attribute.ArgumentList.Arguments[0];
            if (firstArg.NameColon == null &&
                firstArg.Expression is LiteralExpressionSyntax firstLiteral &&
                firstLiteral.Token.IsKind(SyntaxKind.StringLiteralToken))
                return firstLiteral.Token.ValueText;
        }
        return null;
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
                        var propDescription = GetPropertyDescriptionFromAttribute(attribute);
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
                                    isRequired,
                                    propDescription
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
            // Check named argument first
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                if (arg.NameColon?.Name.Identifier.ValueText == "required")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal)
                    {
                        if (literal.Token.IsKind(SyntaxKind.TrueKeyword))
                            return true;
                        if (literal.Token.IsKind(SyntaxKind.FalseKeyword))
                            return false;
                    }
                }
            }

            // Positional: required is 2nd param (index 1) on ShellPropertyAttribute
            for (int i = 0; i < attribute.ArgumentList.Arguments.Count; i++)
            {
                var arg = attribute.ArgumentList.Arguments[i];
                if (arg.NameColon != null)
                    continue;

                if (arg.Expression is LiteralExpressionSyntax literal &&
                    (literal.Token.IsKind(SyntaxKind.TrueKeyword) || literal.Token.IsKind(SyntaxKind.FalseKeyword)))
                {
                    return literal.Token.IsKind(SyntaxKind.TrueKeyword);
                }
            }
        }
        // Default value is true according to the attribute definition
        return true;
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

        if (filtered.IsEmpty)
            return;

        // Generate AddGeneratedMaps and nav extensions only if enabled
        if (options.GenerateNavExtensions)
        {
            GenerateNavigationBuilderExtensions(context, filtered);
            GenerateNavigationExtensions(context, filtered);
            GenerateNavigationBuilderNavExtensions(context, filtered);
            GenerateRouteInfoExtension(context, filtered);
        }
        else
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NavExtensionsDisabledWithMaps,
                Location.None,
                filtered.Length
            ));
        }

        // Generate Routes class only if enabled
        if (options.GenerateRouteConstants)
            GenerateRoutesClass(context, filtered);
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

            // XML doc comment
            if (cls.Description != null)
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// {EscapeXml(cls.Description)}");
                sb.AppendLine($"    /// </summary>");

                foreach (var prop in requiredParams.Concat(optionalParams))
                {
                    var paramDesc = prop.Description != null ? EscapeXml(prop.Description) : "";
                    sb.AppendLine($"    /// <param name=\"{ToCamelCase(prop.Name)}\">{paramDesc}</param>");
                }

                sb.AppendLine($"    /// <param name=\"relativeNavigation\">If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route</param>");
            }

            // [Description] on method
            if (cls.Description != null)
                sb.AppendLine($"    [global::System.ComponentModel.Description(\"{EscapeString(cls.Description)}\")]");

            sb.Append($"    public static global::System.Threading.Tasks.Task {methodName}(this global::Shiny.INavigator navigator");

            // Add required parameters first
            foreach (var prop in requiredParams)
            {
                if (prop.Description != null)
                    sb.Append($", [global::System.ComponentModel.Description(\"{EscapeString(prop.Description)}\")] {prop.TypeName} {ToCamelCase(prop.Name)}");
                else
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)}");
            }

            // Add optional parameters last
            foreach (var prop in optionalParams)
            {
                var defaultValue = GetDefaultValue(prop.TypeName);
                if (prop.Description != null)
                    sb.Append($", [global::System.ComponentModel.Description(\"{EscapeString(prop.Description)}\")] {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
                else
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
            }

            if (cls.Description != null)
                sb.Append(", [global::System.ComponentModel.Description(\"If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route\")] bool relativeNavigation = true");
            else
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

    static void GenerateNavigationBuilderNavExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public static class NavigationBuilderNavExtensions");
        sb.AppendLine("{");

        foreach (var cls in classes)
        {
            var methodName = $"Add{cls.GeneratedName}";
            var requiredParams = cls.Properties.Where(p => p.IsRequired).ToList();
            var optionalParams = cls.Properties.Where(p => !p.IsRequired).ToList();

            if (cls.Properties.Any())
            {
                sb.Append($"    public static global::Shiny.INavigationBuilder {methodName}(this global::Shiny.INavigationBuilder builder");

                foreach (var prop in requiredParams)
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)}");

                foreach (var prop in optionalParams)
                {
                    var defaultValue = GetDefaultValue(prop.TypeName);
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
                }

                sb.AppendLine(")");
                sb.AppendLine("    {");
                sb.Append($"        return builder.Add<{cls.ViewModelFullName}>(x => {{ ");

                var assignments = cls.Properties.Select(p => $"x.{p.Name} = {ToCamelCase(p.Name)}");
                sb.Append(string.Join("; ", assignments));
                sb.Append(";");

                sb.AppendLine(" });");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine($"    public static global::Shiny.INavigationBuilder {methodName}(this global::Shiny.INavigationBuilder builder)");
                sb.AppendLine("    {");
                sb.AppendLine($"        return builder.Add<{cls.ViewModelFullName}>();");
                sb.AppendLine("    }");
            }

            sb.AppendLine();
        }

        sb.AppendLine("}");
        context.AddSource("NavigationBuilderNavExtensions.g.cs", sb.ToString());
    }

    static void GenerateRouteInfoExtension(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public static class GeneratedRouteInfoExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.ComponentModel.Description(\"This provides a list of routes throughout the application\")]");
        sb.AppendLine("    public static global::Shiny.Infrastructure.GeneratedRouteInfo[] GetGeneratedRouteInfo(this global::Shiny.INavigator navigator) =>");
        sb.AppendLine("    [");

        for (int i = 0; i < classes.Length; i++)
        {
            var cls = classes[i];
            var propsWithDesc = cls.Properties.Where(p => p.Description != null).ToList();
            var descriptionArg = cls.Description != null
                ? $"\"{EscapeString(cls.Description)}\""
                : "\"\"";

            sb.Append($"        new global::Shiny.Infrastructure.GeneratedRouteInfo(\"{EscapeString(cls.Route)}\", {descriptionArg}, [");

            if (propsWithDesc.Any())
            {
                var paramEntries = propsWithDesc.Select(p =>
                    $"new global::Shiny.Infrastructure.GeneratedRouteParameter(\"{EscapeString(p.Name)}\", \"{EscapeString(p.Description)}\")");
                sb.Append(string.Join(", ", paramEntries));
            }

            sb.Append("])");
            if (i < classes.Length - 1)
                sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("    ];");
        sb.AppendLine("}");

        context.AddSource("GeneratedRouteInfoExtensions.g.cs", sb.ToString());
    }

    static string ToCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text) || char.IsLower(text[0]))
            return text;
        return char.ToLower(text[0]) + text.Substring(1);
    }

    static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    static string EscapeString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
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
    string Description,
    ImmutableArray<ShellPropertyInfo> Properties,
    Location? AttributeLocation
);

record ShellPropertyInfo(
    string Name,
    string TypeName,
    bool IsRequired,
    string Description
);