using System.Globalization;

namespace NickeltownFinance.Core.Formatting;

/// <summary>Club-wide display formats for calendar dates (Australian style).</summary>
public static class DateFormats
{
    public const string Date = "dd/MM/yyyy";
    public const string DateTime = "dd/MM/yyyy HH:mm";

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Format(DateTime value) =>
        value.ToString(Date, Culture);

    public static string Format(DateTime? value) =>
        value is { } d ? Format(d) : string.Empty;

    public static string FormatDateTime(DateTime value) =>
        value.ToString(DateTime, Culture);

    public static string FormatDateTime(DateTime? value) =>
        value is { } d ? FormatDateTime(d) : string.Empty;
}
