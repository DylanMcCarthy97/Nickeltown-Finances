using NickeltownFinance.Core.Formatting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NickeltownFinance.Reports.Exporters;

public sealed class PitstopManualEntryPdfRequest
{
    public string ClubName { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public DateTime EventDate { get; init; }

    public decimal CashAmount { get; init; }

    public decimal CardAmount { get; init; }

    public string Notes { get; init; } = string.Empty;

    public string RecordedBy { get; init; } = string.Empty;

    public DateTime RecordedAt { get; init; } = DateTime.Now;

    public decimal TotalAmount => CashAmount + CardAmount;
}

public static class PitstopManualEntryPdf
{
    static PitstopManualEntryPdf() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Generate(PitstopManualEntryPdfRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(string.IsNullOrWhiteSpace(request.ClubName)
                            ? "Nickeltown Flounderers Auto Club"
                            : request.ClubName)
                        .Bold().FontSize(16).FontColor(Colors.Black);
                    col.Item().AlignCenter().Text("Pitstop event summary").SemiBold().FontSize(13);
                    col.Item().AlignCenter().Text(DateFormats.Format(request.EventDate))
                        .FontSize(11).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().Column(col =>
                {
                    col.Item().Text(string.IsNullOrWhiteSpace(request.Title)
                            ? "Pitstop event"
                            : request.Title)
                        .Bold().FontSize(14).FontColor(Colors.Black);

                    col.Item().PaddingTop(4)
                        .Text("Manual entry — ClubPOS did not generate an end-of-day report. Cash and card amounts were recorded by the treasurer.")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);

                    col.Item().PaddingTop(16).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.ConstantColumn(120);
                        });

                        AmountRow(table, "Cash", request.CashAmount, bold: false);
                        AmountRow(table, "Card", request.CardAmount, bold: false);
                        AmountRow(table, "Total takings", request.TotalAmount, bold: true);
                    });

                    if (!string.IsNullOrWhiteSpace(request.Notes))
                    {
                        col.Item().PaddingTop(16).Text("Notes").Bold().FontSize(11).FontColor(Colors.Black);
                        col.Item().PaddingTop(4).Text(request.Notes.Trim());
                    }

                    col.Item().PaddingTop(24).Text("Recorded by").FontSize(8).FontColor(Colors.Grey.Medium);
                    col.Item().Text(string.IsNullOrWhiteSpace(request.RecordedBy) ? "Treasurer" : request.RecordedBy)
                        .SemiBold();
                    col.Item().Text(DateFormats.FormatDateTime(request.RecordedAt))
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                });

                page.Footer().AlignCenter()
                    .DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium))
                    .Text("Nickeltown Finance  ·  Manual Pitstop entry");
            });
        }).GeneratePdf();
    }

    private static void AmountRow(TableDescriptor table, string label, decimal amount, bool bold)
    {
        table.Cell().PaddingVertical(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .Text(label).SemiBold().FontColor(Colors.Black);
        table.Cell().PaddingVertical(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .AlignRight().Text(text =>
            {
                if (bold)
                    text.Span(amount.ToString("C")).Bold().FontColor(Colors.Black);
                else
                    text.Span(amount.ToString("C")).FontColor(Colors.Black);
            });
    }
}
