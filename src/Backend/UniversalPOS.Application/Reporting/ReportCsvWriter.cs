using System.Globalization;
using System.Reflection;
using System.Text;

namespace UniversalPOS.Application.Reporting;

/// <summary>
/// A minimal, dependency-free CSV writer for report rows — reflects over a flat DTO's
/// public properties for the header row, so every report DTO gets CSV export for free
/// without hand-writing a serializer per report.
/// </summary>
public static class ReportCsvWriter
{
    public static string Write<T>(IEnumerable<T> rows)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(',', properties.Select(p => EscapeField(p.Name))));

        foreach (var row in rows)
        {
            var values = properties.Select(p => FormatValue(p.GetValue(row)));
            sb.AppendLine(string.Join(',', values.Select(EscapeField)));
        }

        return sb.ToString();
    }

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal dec => dec.ToString(CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string EscapeField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
