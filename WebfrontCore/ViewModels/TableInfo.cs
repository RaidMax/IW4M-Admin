using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Html;

namespace WebfrontCore.ViewModels;

public class TableInfo
{
    public string Header { get; set; }
    public List<ColumnDefinition> Columns { get; } = new();
    public List<RowDefinition> Rows { get; } = new();
    public int InitialRowCount { get; }

    public TableInfo(int initialRowCount = 0)
    {
        InitialRowCount = initialRowCount;
    }
}

public class RowDefinition
{
    public List<ColumnTypeDefinition> Datum { get; } = new();
}

public class ColumnDefinition
{
    public string Title { get; set; }
    public string ColumnSpan { get; set; }
}

public enum ColumnType
{
    Text,
    Link,
    Icon,
    Button
}

public class ColumnTypeDefinition
{
    public ColumnType Type { get; set; }
    public string Value { get; set; }
    public string Data { get; set; }
    public IHtmlContent Template { get; set; }
    public int Id { get; set; }
}

public static class TableInfoExtensions
{
    public static TableInfo WithColumns(this TableInfo info, IEnumerable<string> columns)
    {
        info.Columns.AddRange(columns.Select(column => new ColumnDefinition
        {
            Title = column
        }));

        return info;
    }

    public static TableInfo WithRows<T>(this TableInfo info, IEnumerable<T> source,
        Func<T, IEnumerable<string>> selector)
    {
        return WithRows(info, source, (outer) => selector(outer).Select(item => new ColumnTypeDefinition
        {
            Value = item,
            Type = ColumnType.Text
        }));
    }

    public static TableInfo WithRows<T>(this TableInfo info, IEnumerable<T> source,
        Func<T, IEnumerable<ColumnTypeDefinition>> selector)
    {
        info.Rows.AddRange(source.Select(row =>
        {
            var rowDef = new RowDefinition();
            rowDef.Datum.AddRange(selector(row));
            return rowDef;
        }));
        return info;
    }
}
