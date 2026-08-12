using ClosedXML.Excel;
using NickeltownFinance.Core.DTOs;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NickeltownFinance.Reports.Exporters;

public static class MonthlyReportExporter
{
    static MonthlyReportExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public static string ExportPdf(MonthlyReportData data, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var tempDir = Path.Combine(Path.GetTempPath(), "NickeltownFinance", "monthly-export", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mainPdfPath = Path.Combine(tempDir, "monthly-report.pdf");
            GenerateMainPdf(data, mainPdfPath);

            var appendixPdfs = BuildPitstopAppendixPdfs(data, tempDir);
            if (appendixPdfs.Count == 0)
            {
                File.Copy(mainPdfPath, outputPath, overwrite: true);
            }
            else
            {
                MergePdfs([mainPdfPath, ..appendixPdfs], outputPath);
            }

            return outputPath;
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of temp export files.
            }
        }
    }

    private static void GenerateMainPdf(MonthlyReportData data, string outputPath)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"monthly-test-{Guid.NewGuid():N}.pdf");
        try
        {
            GenerateMonthlyPdfWithFontSize(data, tempPath, baseFontSize: 9);
            
            var pageCount = CountPdfPages(tempPath);
            if (pageCount > 1)
            {
                var enlargedFontSize = 10;
                GenerateMonthlyPdfWithFontSize(data, outputPath, baseFontSize: enlargedFontSize);
            }
            else
            {
                File.Copy(tempPath, outputPath, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private static void GenerateMonthlyPdfWithFontSize(MonthlyReportData data, string outputPath, int baseFontSize)
    {
        var headerTitleSize = baseFontSize + 7;
        var headerSubtitleSize = baseFontSize + 3;
        var headerMetaSize = baseFontSize + 1;
        var sectionHeaderSize = baseFontSize + 2;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(baseFontSize).FontColor(Colors.Grey.Darken3));

                page.Header().Element(c => ComposeHeader(c, data, headerTitleSize, headerSubtitleSize, headerMetaSize));
                page.Content().Element(c => ComposeContent(c, data, baseFontSize, sectionHeaderSize));
                page.Footer().Element(c => ComposeFooter(c, data, baseFontSize - 1));
            });
        }).GeneratePdf(outputPath);
    }

    private static int CountPdfPages(string pdfPath)
    {
        using var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.InformationOnly);
        return doc.PageCount;
    }

    private static void ComposeHeader(IContainer container, MonthlyReportData data, int titleSize, int subtitleSize, int metaSize)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(data.LogoPath) && File.Exists(data.LogoPath))
                col.Item().AlignCenter().Height(44).Width(90).Image(data.LogoPath).FitArea();

            col.Item().AlignCenter().Text(data.ClubName).Bold().FontSize(titleSize).FontColor(Colors.Black);
            col.Item().AlignCenter().Text("Monthly Treasurer Report").SemiBold().FontSize(subtitleSize);
            col.Item().AlignCenter().Text($"{data.PeriodLabel}  ·  Financial Year {data.FinancialYearName}")
                .FontSize(metaSize).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(2).AlignCenter()
                .Text($"Signed by {data.PreparedBy} ({data.PreparedByRole})  ·  {data.PrintedAtDisplay}")
                .FontSize(metaSize - 2).FontColor(Colors.Grey.Medium);
            col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);
        });
    }

    private static void ComposeContent(IContainer container, MonthlyReportData data, int baseFontSize, int sectionHeaderSize)
    {
        var summaryCellLabelSize = baseFontSize - 2;
        var summaryCellValueSize = baseFontSize + 1;
        var categoryBlockTitleSize = baseFontSize + 1;
        var squareItemSize = baseFontSize;
        var tableHeaderSize = baseFontSize - 2;
        var tableCellSize = baseFontSize - 2;
        var squareDetailSize = tableCellSize;

        container.Column(col =>
        {
            // Summary strip
            col.Item().Background(Colors.Grey.Lighten4).Padding(8).Row(row =>
            {
                SummaryCell(row, "Opening balance", data.OpeningBalance, summaryCellLabelSize, summaryCellValueSize);
                SummaryCell(row, "Income", data.TotalIncome, summaryCellLabelSize, summaryCellValueSize);
                SummaryCell(row, "Expenses", data.TotalExpenses, summaryCellLabelSize, summaryCellValueSize);
                SummaryCell(row, data.ClosingBalanceTitle, data.ClosingBalance, summaryCellLabelSize, summaryCellValueSize, bold: true);
            });

            col.Item().PaddingTop(8).Text("Category summary").Bold().FontSize(sectionHeaderSize).FontColor(Colors.Black);

            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Element(c => CategoryBlock(c, "Income", data.IncomeByCategory, data.TotalIncome, categoryBlockTitleSize, baseFontSize));
                row.ConstantItem(12);
                row.RelativeItem().Element(c => CategoryBlock(c, "Expenses", data.ExpensesByCategory, data.TotalExpenses, categoryBlockTitleSize, baseFontSize));
            });

            col.Item().PaddingTop(6).Row(r =>
            {
                r.RelativeItem().Text("Monthly profit / loss").Bold();
                r.ConstantItem(100).AlignRight().Text(data.MonthlyProfit.ToString("C")).Bold();
            });

            col.Item().PaddingTop(8).Text("Treasurer comments").Bold().FontSize(sectionHeaderSize).FontColor(Colors.Black);
            col.Item().PaddingTop(2).Row(r =>
            {
                r.RelativeItem().Text("Excludes petty cash — cash on hand");
                r.ConstantItem(100).AlignRight().Text(data.CashOnHand.ToString("C"));
            });
            col.Item().Row(r =>
            {
                r.RelativeItem().Text("Bonds held with the Shire");
                r.ConstantItem(100).AlignRight().Text(data.ShireBonds.ToString("C"));
            });
            if (data.PayPalBalance > 0)
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("PayPal account balance");
                    r.ConstantItem(100).AlignRight().Text(data.PayPalBalance.ToString("C"));
                });
            }
            col.Item().PaddingTop(2).Row(r =>
            {
                r.RelativeItem().Text("Total moneys owned by the club").Bold();
                r.ConstantItem(100).AlignRight().Text(data.TotalFundsOwned.ToString("C")).Bold();
            });

            // Square breakdown for matched deposits
            if (data.HasSquareBreakdown)
            {
                col.Item().PaddingTop(8).Text("Square transfer breakdown").Bold().FontSize(sectionHeaderSize).FontColor(Colors.Black);
                col.Item().PaddingTop(1).Text("What made up the matched Square deposits this month.")
                    .FontSize(baseFontSize - 1).FontColor(Colors.Grey.Medium);

                foreach (var section in data.SquareBreakdown)
                {
                    col.Item().PaddingTop(4).Text(section.SectionName).SemiBold().FontSize(squareItemSize);
                    foreach (var item in section.Items)
                    {
                        col.Item().PaddingTop(1).PaddingLeft(8).Row(r =>
                        {
                            r.RelativeItem().Text(item.Label);
                            r.ConstantItem(72).AlignRight().Text(item.Amount > 0 ? item.Amount.ToString("C") : "—");
                        });
                    }

                    col.Item().PaddingTop(1).PaddingLeft(8).Row(r =>
                    {
                        r.RelativeItem().Text("Section total").SemiBold();
                        r.ConstantItem(72).AlignRight().Text(section.SectionTotal.ToString("C")).SemiBold();
                    });
                }
            }

            // Transaction detail — bank descriptions
            col.Item().PaddingTop(8).Text("Bank transactions").Bold().FontSize(sectionHeaderSize).FontColor(Colors.Black);
            col.Item().Text("As shown on the ANZ statement, with the category assigned in Nickeltown Finance.")
                .FontSize(baseFontSize - 1).FontColor(Colors.Grey.Medium);

            if (data.Transactions.Count == 0)
            {
                col.Item().PaddingTop(4).Text("No transactions in this month.").Italic().FontColor(Colors.Grey.Medium);
            }
            else
            {
                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(72);
                        columns.RelativeColumn(2.4f);
                        columns.RelativeColumn(1.2f);
                        columns.ConstantColumn(68);
                        columns.ConstantColumn(68);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(c => HeaderCell(c, tableHeaderSize)).Text("Date");
                        header.Cell().Element(c => HeaderCell(c, tableHeaderSize)).Text("Bank description (ANZ)");
                        header.Cell().Element(c => HeaderCell(c, tableHeaderSize)).Text("Category");
                        header.Cell().Element(c => HeaderCell(c, tableHeaderSize)).AlignRight().Text("Money in");
                        header.Cell().Element(c => HeaderCell(c, tableHeaderSize)).AlignRight().Text("Money out");
                    });

                    var alt = false;
                    foreach (var txn in data.Transactions)
                    {
                        var bg = alt ? Colors.Grey.Lighten4 : Colors.White;
                        alt = !alt;

                        table.Cell().Element(c => BodyCell(c, bg, tableCellSize)).Text(txn.Date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
                        table.Cell().Element(c => BodyCell(c, bg, tableCellSize)).Text(txn.Description);
                        table.Cell().Element(c => BodyCell(c, bg, tableCellSize)).Text(txn.CategoryName);
                        table.Cell().Element(c => BodyCell(c, bg, tableCellSize)).AlignRight()
                            .Text(txn.MoneyIn > 0 ? txn.MoneyIn.ToString("C") : "—");
                        table.Cell().Element(c => BodyCell(c, bg, tableCellSize)).AlignRight()
                            .Text(txn.MoneyOut > 0 ? txn.MoneyOut.ToString("C") : "—");

                        if (txn.HasSquareItems)
                        {
                            foreach (var item in txn.SquareItems)
                            {
                                table.Cell().Element(c => BodyCell(c, bg, squareDetailSize)).Text("").FontSize(squareDetailSize);
                                table.Cell().Element(c => BodyCell(c, bg, squareDetailSize)).PaddingLeft(10)
                                    .Text($"↳ {item.Label}").FontSize(squareDetailSize).FontColor(Colors.Grey.Darken1);
                                table.Cell().Element(c => BodyCell(c, bg, squareDetailSize)).Text("Square").FontSize(squareDetailSize).FontColor(Colors.Grey.Medium);
                                table.Cell().Element(c => BodyCell(c, bg, squareDetailSize)).AlignRight()
                                    .Text(item.Amount > 0 ? item.Amount.ToString("C") : "—").FontSize(squareDetailSize);
                                table.Cell().Element(c => BodyCell(c, bg, squareDetailSize)).Text("—").FontSize(squareDetailSize);
                            }
                        }
                    }
                });

                col.Item().PaddingTop(4).AlignRight()
                    .Text($"{data.TransactionCount} transaction(s)")
                    .FontSize(baseFontSize).FontColor(Colors.Grey.Medium);
            }

            if (!string.IsNullOrWhiteSpace(data.Notes))
            {
                col.Item().PaddingTop(8).Text("Notes").Bold().FontSize(sectionHeaderSize).FontColor(Colors.Black);
                col.Item().Text(data.Notes);
            }

            // Keep declaration with the report body when it fits (avoids a signature-only page).
            col.Item().PaddingTop(10).ShowEntire().Element(c => ComposeSignatureBlock(c, data, baseFontSize));
        });
    }

    private static IReadOnlyList<string> BuildPitstopAppendixPdfs(MonthlyReportData data, string tempDir)
    {
        if (!data.HasPitstopReports)
            return [];

        var paths = new List<string>();
        foreach (var report in data.PitstopReports)
        {
            if (IsPitstopPdf(report) && File.Exists(report.FullPath))
            {
                paths.Add(report.FullPath);
                continue;
            }

            var imagePdf = CreateImageAppendixPdf(report, tempDir);
            if (imagePdf is not null)
                paths.Add(imagePdf);
        }

        return paths;
    }

    private static bool IsPitstopPdf(MonthDocumentInfo report) =>
        report.IsPdf
        || Path.GetExtension(report.FullPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
        || Path.GetExtension(report.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string? CreateImageAppendixPdf(MonthDocumentInfo report, string tempDir)
    {
        var images = ResolvePitstopExportImages(report);
        if (images.Count == 0)
            return null;

        var path = Path.Combine(tempDir, $"{Guid.NewGuid():N}.pdf");
        Document.Create(container =>
        {
            for (var i = 0; i < images.Count; i++)
            {
                var imagePath = images[i];
                var pageIndex = i;
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                    page.Content().Column(col =>
                    {
                        if (pageIndex == 0)
                        {
                            col.Item().Text(report.DisplayLabel).Bold().FontSize(12).FontColor(Colors.Black);
                            col.Item().PaddingTop(2).Text(report.FileName).FontSize(9).FontColor(Colors.Grey.Medium);
                        }

                        if (images.Count > 1)
                        {
                            col.Item().PaddingTop(pageIndex == 0 ? 8 : 0)
                                .Text($"Page {pageIndex + 1} of {images.Count}")
                                .FontSize(8).FontColor(Colors.Grey.Medium);
                        }

                        var maxHeight = pageIndex == 0 ? 680 : 740;
                        col.Item().PaddingTop(6).MaxHeight(maxHeight).Image(imagePath).FitArea();
                    });
                });
            }
        }).GeneratePdf(path);

        return path;
    }

    private static IReadOnlyList<string> ResolvePitstopExportImages(MonthDocumentInfo report)
    {
        var fromPreviews = report.PreviewFullPaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path) && IsQuestPdfSupportedImage(path))
            .ToList();
        if (fromPreviews.Count > 0)
            return fromPreviews;

        if (!string.IsNullOrWhiteSpace(report.FullPath)
            && File.Exists(report.FullPath)
            && IsQuestPdfSupportedImage(report.FullPath))
        {
            return [report.FullPath];
        }

        if (!string.IsNullOrWhiteSpace(report.ThumbnailFullPath)
            && File.Exists(report.ThumbnailFullPath)
            && IsQuestPdfSupportedImage(report.ThumbnailFullPath))
        {
            return [report.ThumbnailFullPath];
        }

        return [];
    }

    private static bool IsQuestPdfSupportedImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp";
    }

    private static void MergePdfs(IReadOnlyList<string> pdfPaths, string outputPath)
    {
        using var output = new PdfDocument();
        foreach (var path in pdfPaths)
        {
            using var input = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            for (var i = 0; i < input.PageCount; i++)
                output.AddPage(input.Pages[i]);
        }

        output.Save(outputPath);
    }

    private static void ComposeSignatureBlock(IContainer container, MonthlyReportData data, int baseFontSize)
    {
        var titleSize = baseFontSize + 1;
        var textSize = baseFontSize - 1;
        var labelSize = baseFontSize - 2;
        var nameSize = baseFontSize + 1;

        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
        {
            col.Item().Text("Treasurer declaration").Bold().FontSize(titleSize).FontColor(Colors.Black);
            col.Item().PaddingTop(2)
                .Text("I confirm this report is a true and fair summary of the club's bank transactions for the period.")
                .FontSize(textSize).FontColor(Colors.Grey.Darken1);

            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Full name").FontSize(labelSize).FontColor(Colors.Grey.Medium);
                    c.Item().PaddingTop(4).MinHeight(36).AlignBottom().Column(inner =>
                    {
                        inner.Item().Text(data.PreparedBy).SemiBold().FontSize(nameSize);
                        inner.Item().Text(data.PreparedByRole).FontSize(labelSize).FontColor(Colors.Grey.Medium);
                    });
                    c.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(16);

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Signature").FontSize(labelSize).FontColor(Colors.Grey.Medium);
                    c.Item().PaddingTop(4).MinHeight(36).AlignBottom().Column(inner =>
                    {
                        if (data.HasSignature)
                        {
                            inner.Item().Height(28).Width(120).Image(data.SignatureImagePath!).FitArea();
                            inner.Item().PaddingTop(2).Text("Digitally signed").FontSize(labelSize).FontColor(Colors.Grey.Medium);
                        }
                        else
                        {
                            inner.Item().Text("Draw signature in Settings").FontSize(labelSize).FontColor(Colors.Grey.Medium);
                        }
                    });
                    c.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(16);

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Date").FontSize(labelSize).FontColor(Colors.Grey.Medium);
                    c.Item().PaddingTop(4).MinHeight(36).AlignBottom()
                        .Text(data.PrintedAt.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)).SemiBold().FontSize(nameSize);
                    c.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    private static void ComposeFooter(IContainer container, MonthlyReportData data, int footerFontSize)
    {
        container.AlignCenter().DefaultTextStyle(x => x.FontSize(footerFontSize).FontColor(Colors.Grey.Medium)).Text(t =>
        {
            t.Span("Nickeltown Finance  ·  ");
            t.Span($"Signed by {data.PreparedBy}  ·  ");
            t.Span(data.PrintedAtDisplay);
            t.Span("  ·  Page ");
            t.CurrentPageNumber();
            t.Span(" of ");
            t.TotalPages();
        });
    }

    private static void SummaryCell(RowDescriptor row, string label, decimal amount, int labelSize, int valueSize, bool bold = false)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(label).FontSize(labelSize).FontColor(Colors.Grey.Darken1);
            if (bold)
                c.Item().Text(amount.ToString("C")).FontSize(valueSize).Bold();
            else
                c.Item().Text(amount.ToString("C")).FontSize(valueSize);
        });
    }

    private static void CategoryBlock(
        IContainer container,
        string title,
        IReadOnlyList<CategoryTotal> items,
        decimal total,
        int titleSize,
        int itemSize)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(col =>
        {
            col.Item().Text(title).Bold().FontSize(titleSize).FontColor(Colors.Black);
            if (items.Count == 0)
            {
                col.Item().PaddingTop(4).Text("None").Italic().FontColor(Colors.Grey.Medium);
            }
            else
            {
                foreach (var item in items)
                {
                    col.Item().PaddingTop(3).Row(r =>
                    {
                        r.RelativeItem().Text(item.CategoryName).FontSize(itemSize);
                        r.ConstantItem(72).AlignRight().Text(item.Amount.ToString("C")).FontSize(itemSize);
                    });
                }
            }

            col.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            col.Item().PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text($"Total {title.ToLowerInvariant()}").Bold().FontSize(itemSize);
                r.ConstantItem(72).AlignRight().Text(total.ToString("C")).Bold().FontSize(itemSize);
            });
        });
    }

    private static IContainer HeaderCell(IContainer container, int fontSize) =>
        container.DefaultTextStyle(x => x.SemiBold().FontSize(fontSize).FontColor(Colors.White))
            .Background(Colors.Blue.Darken2)
            .PaddingVertical(3)
            .PaddingHorizontal(3);

    private static IContainer BodyCell(IContainer container, string background, int fontSize) =>
        container.Background(background)
            .PaddingVertical(2)
            .PaddingHorizontal(3)
            .DefaultTextStyle(x => x.FontSize(fontSize));

    public static string ExportExcel(MonthlyReportData data, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Summary");

        ws.Cell(1, 1).Value = data.ClubName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(2, 1).Value = $"Monthly Treasurer Report — {data.PeriodLabel}";
        ws.Cell(3, 1).Value = $"Financial Year: {data.FinancialYearName}";
        ws.Cell(4, 1).Value = $"Signed by: {data.PreparedBy} ({data.PreparedByRole})";
        ws.Cell(5, 1).Value = $"Printed: {data.PrintedAtDisplay}";

        var row = 7;
        ws.Cell(row, 1).Value = "Opening Balance";
        ws.Cell(row, 2).Value = data.OpeningBalance;
        ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
        row += 2;

        ws.Cell(row, 1).Value = "INCOME BY CATEGORY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;
        foreach (var item in data.IncomeByCategory)
        {
            ws.Cell(row, 1).Value = item.CategoryName;
            ws.Cell(row, 2).Value = item.Amount;
            ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
            row++;
        }
        ws.Cell(row, 1).Value = "Total Income";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = data.TotalIncome;
        ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
        row += 2;

        ws.Cell(row, 1).Value = "EXPENSES BY CATEGORY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;
        foreach (var item in data.ExpensesByCategory)
        {
            ws.Cell(row, 1).Value = item.CategoryName;
            ws.Cell(row, 2).Value = item.Amount;
            ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
            row++;
        }
        ws.Cell(row, 1).Value = "Total Expenses";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = data.TotalExpenses;
        ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
        row += 2;

        ws.Cell(row, 1).Value = "Monthly Profit";
        ws.Cell(row, 2).Value = data.MonthlyProfit;
        ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
        row++;
        ws.Cell(row, 1).Value = data.ClosingBalanceTitle;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = data.ClosingBalance;
        ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Cash on hand (petty cash)";
        ws.Cell(row, 2).Value = data.CashOnHand;
        ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Bonds with the Shire";
        ws.Cell(row, 2).Value = data.ShireBonds;
        ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
        row++;
        if (data.PayPalBalance > 0)
        {
            ws.Cell(row, 1).Value = "PayPal balance";
            ws.Cell(row, 2).Value = data.PayPalBalance;
            ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
            row++;
        }
        ws.Cell(row, 1).Value = "Total moneys owned by the club";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = data.TotalFundsOwned;
        ws.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
        row += 2;

        if (!string.IsNullOrWhiteSpace(data.Notes))
        {
            ws.Cell(row, 1).Value = "Notes";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 1).Value = data.Notes;
            row += 2;
        }

        ws.Cell(row, 1).Value = "Treasurer signature:";
        ws.Cell(row, 2).Value = "________________________";
        row++;
        ws.Cell(row, 1).Value = "Date:";
        ws.Cell(row, 2).Value = "________________________";

        ws.Columns().AdjustToContents();

        var tx = wb.Worksheets.Add("Bank transactions");
        tx.Cell(1, 1).Value = "Date";
        tx.Cell(1, 2).Value = "Bank description (ANZ)";
        tx.Cell(1, 3).Value = "Category";
        tx.Cell(1, 4).Value = "Money in";
        tx.Cell(1, 5).Value = "Money out";
        tx.Range(1, 1, 1, 5).Style.Font.Bold = true;

        var txRow = 2;
        foreach (var item in data.Transactions)
        {
            tx.Cell(txRow, 1).Value = item.Date;
            tx.Cell(txRow, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            tx.Cell(txRow, 2).Value = item.Description;
            tx.Cell(txRow, 3).Value = item.CategoryName;
            if (item.MoneyIn > 0)
            {
                tx.Cell(txRow, 4).Value = item.MoneyIn;
                tx.Cell(txRow, 4).Style.NumberFormat.Format = "$#,##0.00";
            }

            if (item.MoneyOut > 0)
            {
                tx.Cell(txRow, 5).Value = item.MoneyOut;
                tx.Cell(txRow, 5).Style.NumberFormat.Format = "$#,##0.00";
            }
            txRow++;
        }

        tx.Columns().AdjustToContents();

        if (data.HasSquareBreakdown)
        {
            var sq = wb.Worksheets.Add("Square breakdown");
            sq.Cell(1, 1).Value = "Section";
            sq.Cell(1, 2).Value = "Item";
            sq.Cell(1, 3).Value = "Quantity";
            sq.Cell(1, 4).Value = "Amount";
            sq.Range(1, 1, 1, 4).Style.Font.Bold = true;

            var sqRow = 2;
            foreach (var section in data.SquareBreakdown)
            {
                foreach (var item in section.Items)
                {
                    sq.Cell(sqRow, 1).Value = section.SectionName;
                    sq.Cell(sqRow, 2).Value = item.ItemName;
                    sq.Cell(sqRow, 3).Value = item.Quantity;
                    sq.Cell(sqRow, 4).Value = item.Amount;
                    sq.Cell(sqRow, 4).Style.NumberFormat.Format = "$#,##0.00";
                    sqRow++;
                }

                sq.Cell(sqRow, 1).Value = section.SectionName;
                sq.Cell(sqRow, 2).Value = "Section total";
                sq.Cell(sqRow, 4).Value = section.SectionTotal;
                sq.Cell(sqRow, 4).Style.NumberFormat.Format = "$#,##0.00";
                sq.Row(sqRow).Style.Font.Bold = true;
                sqRow += 2;
            }

            sq.Columns().AdjustToContents();
        }

        if (data.HasPitstopReports)
        {
            var pit = wb.Worksheets.Add("Pitstop reports");
            pit.Cell(1, 1).Value = "Title";
            pit.Cell(1, 2).Value = "File name";
            pit.Cell(1, 3).Value = "Pages";
            pit.Cell(1, 4).Value = "Added";
            pit.Cell(1, 5).Value = "Added by";
            pit.Range(1, 1, 1, 5).Style.Font.Bold = true;

            var pitRow = 2;
            foreach (var report in data.PitstopReports)
            {
                pit.Cell(pitRow, 1).Value = report.DisplayLabel;
                pit.Cell(pitRow, 2).Value = report.FileName;
                pit.Cell(pitRow, 3).Value = Math.Max(1, report.PageCount);
                pit.Cell(pitRow, 4).Value = report.DateAdded.ToLocalTime();
                pit.Cell(pitRow, 4).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                pit.Cell(pitRow, 5).Value = report.AddedByName;
                pitRow++;
            }

            pit.Cell(pitRow + 1, 1).Value =
                "Full report pages are appended to the PDF export of this monthly report.";
            pit.Columns().AdjustToContents();
        }

        wb.SaveAs(outputPath);
        return outputPath;
    }
}
