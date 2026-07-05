using ClosedXML.Excel;
using NickeltownFinance.Core.DTOs;

namespace NickeltownFinance.Infrastructure.Import;

/// <summary>
/// Converts an Excel sheet to CSV text and parses with the ANZ headerless format.
/// </summary>
public static class AnzExcelStatementParser
{
    public static StatementParseResult Parse(string filePath, AnzColumnMapping? mapping = null)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed();
        if (used is null)
            throw new InvalidOperationException("The Excel file has no data.");

        var tempPath = Path.Combine(Path.GetTempPath(), $"nickeltown_anz_{Guid.NewGuid():N}.csv");
        try
        {
            using (var writer = new StreamWriter(tempPath))
            {
                foreach (var row in used.RowsUsed())
                {
                    var cells = row.Cells(1, used.ColumnCount()).Select(c => EscapeCsv(GetCellText(c)));
                    writer.WriteLine(string.Join(',', cells));
                }
            }

            var result = AnzCsvStatementParser.Parse(tempPath, mapping);
            return new StatementParseResult
            {
                Format = Core.Enums.StatementFormat.Excel,
                BankName = result.BankName,
                FileName = Path.GetFileName(filePath),
                Rows = result.Rows,
                Warnings = result.Warnings
            };
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* ignore */ }
        }
    }

    private static string GetCellText(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().ToString("dd/MM/yyyy");

        if (cell.DataType == XLDataType.Number)
            return cell.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);

        return cell.GetString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
