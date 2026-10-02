using System;
using System.Collections.Generic;
using System.Text;

namespace TurnLimbo.Runtime.Sheets
{
    public sealed class CsvFormatException : FormatException
    {
        public CsvFormatException(int rowNumber, string message) : base($"{rowNumber}행: {message}")
        {
            RowNumber = rowNumber;
            Detail = message;
        }

        /// <summary>The 1-based record the problem is in, as a spreadsheet numbers its rows.</summary>
        public int RowNumber { get; }
        /// <summary>The problem without the row prefix.</summary>
        public string Detail { get; }
    }

    /// <summary>One CSV record: a spreadsheet row, even when a quoted cell spans several lines.</summary>
    public sealed class CsvRecord
    {
        private readonly string[] fields;

        internal CsvRecord(int rowNumber, string[] fields)
        {
            RowNumber = rowNumber;
            this.fields = fields;
        }

        /// <summary>1-based; the first record is row 1.</summary>
        public int RowNumber { get; }
        public int Count => fields.Length;
        /// <summary>The field at <paramref name="index"/>, or empty past the end of a short row.</summary>
        public string this[int index] => index >= 0 && index < fields.Length ? fields[index] : string.Empty;
        public IReadOnlyList<string> Fields => fields;
    }

    /// <summary>RFC 4180 comma-separated values: fields with a comma, quote or line break are quoted and quotes are
    /// doubled. Reading accepts CRLF, LF and CR, drops a leading byte order mark and turns line breaks inside quoted
    /// fields into LF. Fields are returned as written; trimming is the caller's choice.</summary>
    public static class CsvTable
    {
        private static readonly char[] QuotedCharacters = { ',', '"', '\n', '\r' };

        public static IReadOnlyList<CsvRecord> Read(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var records = new List<CsvRecord>();
            var fields = new List<string>();
            var field = new StringBuilder();
            int index = text.Length > 0 && text[0] == '\ufeff' ? 1 : 0;
            int row = 1;
            // A record is still open after a separator, or once any character of it has been read.
            bool recordOpen = false;
            while (index < text.Length)
            {
                char c = text[index];
                if (c == '"' && field.Length == 0)
                {
                    int quotedRow = row;
                    index++;
                    while (true)
                    {
                        if (index >= text.Length)
                            throw new CsvFormatException(quotedRow, "따옴표(\")로 시작한 칸이 닫히지 않았습니다. 칸 안의 따옴표는 두 번(\"\") 적으세요.");
                        char q = text[index++];
                        if (q == '"')
                        {
                            if (index < text.Length && text[index] == '"')
                            {
                                field.Append('"');
                                index++;
                                continue;
                            }
                            break;
                        }
                        if (q == '\r' || q == '\n')
                        {
                            if (q == '\r' && index < text.Length && text[index] == '\n') index++;
                            field.Append('\n');
                            continue;
                        }
                        field.Append(q);
                    }
                    recordOpen = true;
                    if (index < text.Length && text[index] != ',' && text[index] != '\r' && text[index] != '\n')
                        throw new CsvFormatException(quotedRow, "따옴표로 감싼 칸이 닫힌 뒤에 쉼표 없이 글자가 이어집니다.");
                    continue;
                }
                index++;
                if (c == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    recordOpen = true;
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && index < text.Length && text[index] == '\n') index++;
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add(new CsvRecord(row++, fields.ToArray()));
                    fields.Clear();
                    recordOpen = false;
                }
                else
                {
                    field.Append(c);
                    recordOpen = true;
                }
            }
            // The last line break is optional.
            if (recordOpen)
            {
                fields.Add(field.ToString());
                records.Add(new CsvRecord(row, fields.ToArray()));
            }
            return records;
        }

        /// <summary>One line per row, each ending in LF, without a byte order mark. Null fields write as empty.</summary>
        public static string Write(IEnumerable<IReadOnlyList<string>> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var text = new StringBuilder();
            foreach (IReadOnlyList<string> row in rows)
            {
                if (row == null) throw new ArgumentException("A row cannot be null.", nameof(rows));
                for (int index = 0; index < row.Count; index++)
                {
                    if (index > 0) text.Append(',');
                    AppendField(text, row[index] ?? string.Empty);
                }
                text.Append('\n');
            }
            return text.ToString();
        }

        private static void AppendField(StringBuilder text, string field)
        {
            if (field.IndexOfAny(QuotedCharacters) < 0)
            {
                text.Append(field);
                return;
            }
            text.Append('"').Append(field.Replace("\"", "\"\"")).Append('"');
        }
    }
}
