namespace NickeltownFinance.Core.DTOs;

/// <summary>
/// Column indices for the ANZ bank CSV export.
/// Default ANZ export (no headers): Date=0, Amount=1, Description=2.
/// </summary>
public class AnzColumnMapping
{
    public int DateIndex { get; set; }

    public int AmountIndex { get; set; } = 1;

    public int DescriptionIndex { get; set; } = 2;

    public int? ReferenceIndex { get; set; }

    public bool SkipFirstRow { get; set; }
}

public class AnzParseFailure
{
    public string Reason { get; set; } = string.Empty;

    public IReadOnlyList<string> SampleLines { get; set; } = [];

    public IReadOnlyList<IReadOnlyList<string>> SampleRows { get; set; } = [];

    public int DetectedColumnCount { get; set; }

    public string FileName { get; set; } = string.Empty;
}

public class ImportSummary
{
    public int TransactionsFound { get; set; }

    public int TransactionsToImport { get; set; }

    public int Duplicates { get; set; }

    public int Ignored { get; set; }

    public decimal EstimatedBalanceChange { get; set; }
}

public class ImportAnalyseResult
{
    public bool Success => Preview is not null && Failure is null;

    public ImportPreview? Preview { get; set; }

    public AnzParseFailure? Failure { get; set; }
}

