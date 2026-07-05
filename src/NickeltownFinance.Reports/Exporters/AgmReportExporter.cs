using ClosedXML.Excel;
using NickeltownFinance.Core.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NickeltownFinance.Reports.Exporters;

public static class AgmReportExporter
{
    static AgmReportExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public static string ExportPdf(AgmReportData data, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    if (!string.IsNullOrWhiteSpace(data.LogoPath) && File.Exists(data.LogoPath))
                        col.Item().AlignCenter().Height(64).Width(120).Image(data.LogoPath).FitArea();
                    col.Item().AlignCenter().Text(data.ClubName).Bold().FontSize(18);
                    col.Item().AlignCenter().Text("Annual General Meeting Report").FontSize(14);
                    col.Item().AlignCenter().Text($"Financial Year {data.FinancialYearName}").FontSize(12);
                    col.Item().AlignCenter()
                        .Text($"Signed by {data.PreparedBy} ({data.PreparedByRole})  ·  {data.PrintedAtDisplay}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Opening Balance").Bold();
                        r.ConstantItem(100).AlignRight().Text(data.OpeningBalance.ToString("C"));
                    });

                    col.Item().PaddingTop(15).Text("MONTHLY SUMMARY").Bold().FontSize(13);
                    col.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text("Month").Bold();
                        r.ConstantItem(80).AlignRight().Text("Income").Bold();
                        r.ConstantItem(80).AlignRight().Text("Expenses").Bold();
                        r.ConstantItem(80).AlignRight().Text("Profit").Bold();
                    });
                    foreach (var m in data.MonthlyData)
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(m.MonthName);
                            r.ConstantItem(80).AlignRight().Text(m.Income.ToString("C"));
                            r.ConstantItem(80).AlignRight().Text(m.Expenses.ToString("C"));
                            r.ConstantItem(80).AlignRight().Text(m.Profit.ToString("C"));
                        });

                    col.Item().PaddingTop(15).Row(r =>
                    {
                        r.RelativeItem().Text("Total Income").Bold();
                        r.ConstantItem(100).AlignRight().Text(data.TotalIncome.ToString("C")).Bold();
                    });
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Total Expenses").Bold();
                        r.ConstantItem(100).AlignRight().Text(data.TotalExpenses.ToString("C")).Bold();
                    });
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Annual Profit").Bold();
                        r.ConstantItem(100).AlignRight().Text(data.AnnualProfit.ToString("C")).Bold();
                    });
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Closing Balance").Bold();
                        r.ConstantItem(100).AlignRight().Text(data.ClosingBalance.ToString("C")).Bold();
                    });

                    col.Item().PaddingTop(15).Text("INCOME BY CATEGORY").Bold();
                    foreach (var item in data.IncomeByCategory)
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(item.CategoryName);
                            r.ConstantItem(100).AlignRight().Text(item.Amount.ToString("C"));
                        });

                    col.Item().PaddingTop(15).Text("EXPENSES BY CATEGORY").Bold();
                    foreach (var item in data.ExpensesByCategory)
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(item.CategoryName);
                            r.ConstantItem(100).AlignRight().Text(item.Amount.ToString("C"));
                        });

                    col.Item().PaddingTop(28).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(14).Column(sig =>
                    {
                        sig.Item().Text("Treasurer declaration").Bold().FontSize(11);
                        sig.Item().PaddingTop(4)
                            .Text("I confirm this report is a true and fair summary of the club's finances for the financial year.")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                        sig.Item().PaddingTop(16).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Full name").FontSize(8).FontColor(Colors.Grey.Medium);
                                c.Item().PaddingTop(8).MinHeight(52).AlignBottom().Column(inner =>
                                {
                                    inner.Item().Text(data.PreparedBy).SemiBold().FontSize(11);
                                    inner.Item().Text(data.PreparedByRole).FontSize(8).FontColor(Colors.Grey.Medium);
                                });
                                c.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                            });
                            row.ConstantItem(20);
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Signature").FontSize(8).FontColor(Colors.Grey.Medium);
                                c.Item().PaddingTop(8).MinHeight(52).AlignBottom().Column(inner =>
                                {
                                    if (data.HasSignature)
                                    {
                                        inner.Item().Height(40).Width(140).Image(data.SignatureImagePath!).FitArea();
                                        inner.Item().PaddingTop(4).Text("Digitally signed").FontSize(8).FontColor(Colors.Grey.Medium);
                                    }
                                    else
                                    {
                                        inner.Item().Text("Draw signature in Settings").FontSize(8).FontColor(Colors.Grey.Medium);
                                    }
                                });
                                c.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                            });
                            row.ConstantItem(20);
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Date").FontSize(8).FontColor(Colors.Grey.Medium);
                                c.Item().PaddingTop(8).MinHeight(52).AlignBottom()
                                    .Text(data.PrintedAt.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)).SemiBold().FontSize(11);
                                c.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                            });
                        });
                    });
                });

                page.Footer().AlignCenter().Text(
                    $"Nickeltown Finance  ·  Signed by {data.PreparedBy}  ·  {data.PrintedAtDisplay}");
            });
        }).GeneratePdf(outputPath);

        return outputPath;
    }

    public static string ExportExcel(AgmReportData data, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("AGM Report");

        ws.Cell(1, 1).Value = data.ClubName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(2, 1).Value = $"AGM Report — Financial Year {data.FinancialYearName}";
        ws.Cell(3, 1).Value = $"Signed by: {data.PreparedBy} ({data.PreparedByRole})";
        ws.Cell(4, 1).Value = $"Printed: {data.PrintedAtDisplay}";

        var row = 6;
        ws.Cell(row, 1).Value = "Opening Balance";
        ws.Cell(row, 4).Value = data.OpeningBalance;
        row += 2;

        ws.Cell(row, 1).Value = "Month";
        ws.Cell(row, 2).Value = "Income";
        ws.Cell(row, 3).Value = "Expenses";
        ws.Cell(row, 4).Value = "Profit";
        ws.Row(row).Style.Font.Bold = true;
        row++;

        foreach (var m in data.MonthlyData)
        {
            ws.Cell(row, 1).Value = m.MonthName;
            ws.Cell(row, 2).Value = m.Income;
            ws.Cell(row, 3).Value = m.Expenses;
            ws.Cell(row, 4).Value = m.Profit;
            row++;
        }

        row++;
        ws.Cell(row, 1).Value = "Total Income";
        ws.Cell(row, 4).Value = data.TotalIncome;
        row++;
        ws.Cell(row, 1).Value = "Total Expenses";
        ws.Cell(row, 4).Value = data.TotalExpenses;
        row++;
        ws.Cell(row, 1).Value = "Annual Profit";
        ws.Cell(row, 4).Value = data.AnnualProfit;
        row++;
        ws.Cell(row, 1).Value = "Closing Balance";
        ws.Cell(row, 4).Value = data.ClosingBalance;
        row += 2;
        ws.Cell(row, 1).Value = "Treasurer signature:";
        ws.Cell(row, 2).Value = "________________________";
        row++;
        ws.Cell(row, 1).Value = "Date:";
        ws.Cell(row, 2).Value = "________________________";

        ws.Columns().AdjustToContents();
        wb.SaveAs(outputPath);
        return outputPath;
    }
}
