using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace NickeltownFinance.Controls;

/// <summary>
/// Professional data grid with design-system defaults and optional column-width memory.
/// </summary>
public class AppDataGrid : DataGrid
{
    private static readonly string LayoutFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NickeltownFinance", "GridLayouts");

    public static readonly DependencyProperty LayoutKeyProperty =
        DependencyProperty.Register(nameof(LayoutKey), typeof(string), typeof(AppDataGrid),
            new PropertyMetadata(null));

    public AppDataGrid()
    {
        AutoGenerateColumns = false;
        CanUserAddRows = false;
        CanUserDeleteRows = false;
        CanUserResizeRows = false;
        CanUserResizeColumns = true;
        CanUserSortColumns = true;
        HeadersVisibility = DataGridHeadersVisibility.Column;
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        SelectionMode = DataGridSelectionMode.Single;
        SelectionUnit = DataGridSelectionUnit.FullRow;
        BorderThickness = new Thickness(0);
        Background = System.Windows.Media.Brushes.Transparent;
        IsReadOnly = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string? LayoutKey
    {
        get => (string?)GetValue(LayoutKeyProperty);
        set => SetValue(LayoutKeyProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestoreColumnWidths();
        foreach (var column in Columns)
            column.Width = column.Width;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => PersistColumnWidths();

    private void RestoreColumnWidths()
    {
        if (string.IsNullOrWhiteSpace(LayoutKey))
            return;

        try
        {
            var path = GetLayoutPath(LayoutKey);
            if (!File.Exists(path))
                return;

            var widths = JsonSerializer.Deserialize<double[]>(File.ReadAllText(path));
            if (widths is null || widths.Length == 0)
                return;

            for (var i = 0; i < Math.Min(widths.Length, Columns.Count); i++)
            {
                if (widths[i] > 24)
                    Columns[i].Width = new DataGridLength(widths[i]);
            }
        }
        catch
        {
            // Layout memory is best-effort only.
        }
    }

    private void PersistColumnWidths()
    {
        if (string.IsNullOrWhiteSpace(LayoutKey) || Columns.Count == 0)
            return;

        try
        {
            Directory.CreateDirectory(LayoutFolder);
            var widths = Columns.Select(c => c.ActualWidth).ToArray();
            File.WriteAllText(GetLayoutPath(LayoutKey), JsonSerializer.Serialize(widths));
        }
        catch
        {
            // Layout memory is best-effort only.
        }
    }

    private static string GetLayoutPath(string key)
    {
        var safe = string.Concat(key.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        return Path.Combine(LayoutFolder, $"{safe}.json");
    }
}
