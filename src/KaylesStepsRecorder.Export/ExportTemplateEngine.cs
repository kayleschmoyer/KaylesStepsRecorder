using System.Reflection;
using System.Text;

namespace KaylesStepsRecorder.Export;

/// <summary>
/// Very small templating engine. Uses <c>{{TOKEN}}</c> placeholders, intentionally
/// avoiding a full templating library since our needs are tiny.
/// </summary>
internal static class ExportTemplateEngine
{
    public static string LoadEmbedded(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        string fullName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found");

        using var stream = asm.GetManifestResourceStream(fullName)
            ?? throw new InvalidOperationException($"Could not open embedded resource stream '{fullName}'");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static string Render(string template, IDictionary<string, string> tokens)
    {
        var sb = new StringBuilder(template);
        foreach (var kvp in tokens)
        {
            sb.Replace("{{" + kvp.Key + "}}", kvp.Value);
        }
        return sb.ToString();
    }

    public static string HtmlEncode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return System.Net.WebUtility.HtmlEncode(value);
    }
}
