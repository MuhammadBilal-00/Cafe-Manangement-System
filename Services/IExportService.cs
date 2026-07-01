using ClosedXML.Excel;

namespace Cafe.Services
{
    /// <summary>Phase 9 (59): one reusable exporter (CSV / Excel) any list screen can call.</summary>
    public interface IExportService
    {
        byte[] ToCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows);
        byte[] ToExcel(string sheetName, IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows);
    }

    public class ExportService : IExportService
    {
        public byte[] ToCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(Escape)));
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row.Select(c => Escape(c?.ToString() ?? ""))));
            return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        }

        public byte[] ToExcel(string sheetName, IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName[..Math.Min(31, sheetName.Length)]);
            var hdr = headers.ToList();
            for (int c = 0; c < hdr.Count; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = hdr[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e2a3a");
                cell.Style.Font.FontColor = XLColor.White;
            }
            int r = 2;
            foreach (var row in rows)
            {
                int c = 1;
                foreach (var val in row)
                {
                    var cell = ws.Cell(r, c++);
                    if (val is decimal dec) cell.Value = dec;
                    else if (val is int i) cell.Value = i;
                    else if (val is double d) cell.Value = d;
                    else if (val is DateTime dt) cell.Value = dt;
                    else cell.Value = val?.ToString() ?? "";
                }
                r++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static string Escape(string v) =>
            v.Contains(',') || v.Contains('"') || v.Contains('\n') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
