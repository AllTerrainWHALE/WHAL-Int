namespace Formatter;

public class Table<T>
{
    private readonly List<TableColumn<T>> columns = [];
    private readonly List<T> dataPoints = [];

    public void AddColumn(
        string title,
        Func<T, string> colFunc,
        int colWidth = 5,
        StringAlignment alignment = StringAlignment.Centered
    ) => columns.Add(new TableColumn<T>(title, colFunc, colWidth, alignment));

    public void AddDataPoint(T dataPoint) => dataPoints.Add(dataPoint);

    public string GetHeader() => string.Join("|", columns.Select(c => c.Name));

    public string GetTable()
    {
        string table = string.Join("\n", dataPoints.Select(x => string.Join("|", columns.Select(f => f.ColumnFunc(x)))));
        return table;
    }
}

public class TableColumn<T>(string name, Func<T, string> colFunc, int width, StringAlignment alignment)
{
    public string Name { get; } = name;
    public Func<T, string> ColumnFunc { get; } = colFunc;
    public int Width { get; } = width;
    public StringAlignment Alignment { get; } = alignment;
}
