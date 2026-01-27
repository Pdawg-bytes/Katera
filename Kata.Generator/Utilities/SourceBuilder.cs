using System;
using System.Text;

namespace Kata.Generator.Utilities;

internal sealed class SourceBuilder
{
    private readonly StringBuilder _sb = new();
    private int _indent;

    internal void SetIndent(int level)
    {
        if (level < 0)
            throw new Exception("SourceBuilder indent level cannot be negative");

        _indent = level;
    }

    internal void Line(string? text = null)
    {
        if (text is null)
        {
            _sb.AppendLine();
            return;
        }

        _sb.Append(' ', _indent * 4);
        _sb.AppendLine(text);
    }

    internal void Write(string text, bool indent)
    {
        if (indent)
            _sb.Append(' ', _indent * 4);

        _sb.Append(text);
    }

    internal void Append(SourceBuilder newSb)
    {
        if (newSb is null)
        {
            _sb.AppendLine();
            return;
        }

        _sb.Append(newSb.ToString());
    }

    internal void OpenBlock(string header)
    {
        Line(header);
        Line("{");
        _indent++;
    }

    internal void CloseBlock(string? trailer = null)
    {
        _indent--;

        if (_indent < 0)
            throw new Exception("SourceBuilder indent is invalid");

        Line("}" + trailer);
    }

    public override string ToString() => _sb.ToString();
}