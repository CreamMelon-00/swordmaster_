using System.IO;
using System.Text;

namespace TurnLimbo.Runtime.Sheets
{
    /// <summary>Reads a sheet file that a spreadsheet program may still have open. Excel keeps an open workbook (CSV
    /// included) open for writing even after saving it, so <see cref="File.ReadAllText(string, Encoding)"/>, which shares
    /// the file for reading only, fails with a sharing violation while Excel is open. This lets the writer stay.</summary>
    public static class SheetFile
    {
        /// <summary>The whole file as text: UTF-8 unless a byte order mark says otherwise, the mark itself left out,
        /// as <see cref="File.ReadAllText(string, Encoding)"/> with <see cref="Encoding.UTF8"/> reads it.</summary>
        public static string ReadAllText(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false), true))
                return reader.ReadToEnd();
        }
    }
}
