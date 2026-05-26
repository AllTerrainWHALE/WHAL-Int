using Newtonsoft.Json;

namespace Formatter;

public class Table<T>
{
    private readonly List<TableColumn<T>> columns = [];
    private readonly List<T> dataPoints = [];
    private string? footer;

    public void AddColumn(
        string title,
        Func<T, string> colFunc,
        int colWidth = 5,
        StringAlignment alignment = StringAlignment.Centered,
        int position = -1
    )
    {
        try
        {
            columns.Insert(
            position < 0 ? columns.Count + 1 + position : position,
            new TableColumn<T>(title, colFunc, colWidth, alignment));
        } 
        catch (Exception ex)
        {
            throw new Exception($"Cannot add to Columns of length {columns.Count} at position {position}.\n\n{ex}");
        }
    }

    public void RemoveColumn(string title) => columns.RemoveAll(c => c.Name == title);
    public void RemoveColumn(int index) => columns.RemoveAt(index);

    public void AddDataPoint(T dataPoint) => dataPoints.Add(dataPoint);

    public List<T> GetDataPoints() => dataPoints;

    public string GetHeader() => string.Join("｜", columns.Select(c => c.Name));

    public string GetTable() =>
        string.Join("\n",
            dataPoints.Select(
                x => string.Join("｜",
                    columns.Select(
                        c => StringFormatter.Align(
                            c.ColumnFunc(x), c.Width, c.Alignment
                        )
                    )
                )
            )
        );

    public void SetFooter(string footer) => this.footer = footer;

    public string GetFooter() => footer ?? "";

    public Table<T> Clone()
    {
        var clone = new Table<T>();

        // Clone columns
        foreach (var col in columns)
        {
            clone.AddColumn(
                col.Name,
                col.ColumnFunc,
                col.Width,
                col.Alignment
            );
        }

        // Clone data points (shallow copy)
        foreach (var dp in dataPoints)
            clone.AddDataPoint(dp);

        // Clone footer
        if (footer is not null)
            clone.SetFooter(footer);

        return clone;
    }
}

public class TableColumn<T>(string name, Func<T, string> colFunc, int width, StringAlignment alignment)
{
    public string Name { get; } = name;
    public Func<T, string> ColumnFunc { get; } = colFunc;
    public int Width { get; } = width;
    public StringAlignment Alignment { get; } = alignment;
}
