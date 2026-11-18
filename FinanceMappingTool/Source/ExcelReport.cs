using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
namespace FinanceMappingTool
{
    public sealed class CustomerReference
    {
        public string FeeNo { get; set; }
        public byte? CustomerType { get; set; }
    }
    public sealed class PreviewRecord
    {
        public MappingRow Data { get; }
        public object[] Cells { get; }
        public PreviewRecord(MappingRow data, object[] cells)
        { Data = data.Copy(); Cells = (object[])cells.Clone(); }
    }
    public sealed class PreviewTable
    {
        public string[] Headers { get; }
        public List<PreviewRecord> Records { get; }
        public PreviewTable(string[] headers, IEnumerable<PreviewRecord> records)
        {
            Headers = (string[])headers.Clone(); Records = records.ToList();
            if (Headers.Length == 0 || Records.Any(r => r.Cells.Length != Headers.Length))
                throw new InvalidDataException("预览列头和数据列数量不一致，无法导出。");
        }
    }
    public sealed class SubjectAnomaly
    {
        public int DataRowNumber { get; }
        public string Description { get; }
        public PreviewRecord Record { get; }
        public SubjectAnomaly(int row, string description, PreviewRecord record)
        { DataRowNumber = row; Description = description; Record = record; }
    }
    public static class ExcelReport
    {
        public const string EmptyOverseasName = "海外科目名称为空";
        public const string CustomerTypeMismatch = "客户\\供应商类型异常";
        public static List<SubjectAnomaly> Analyze(PreviewTable preview, IList<CustomerReference> reference)
        {
            if (reference == null) throw new InvalidDataException("尚未取得KDVoucher客户/供应商参照数据，不能完成异常检查。");
            var byCode = reference.Where(r => !string.IsNullOrWhiteSpace(r.FeeNo)).ToLookup(r => r.FeeNo.Trim(), StringComparer.OrdinalIgnoreCase);
            var result = new List<SubjectAnomaly>();
            for (int i = 0; i < preview.Records.Count; i++)
            {
                var item = preview.Records[i]; var data = item.Data; int sheetRow = i + 2;
                if (string.IsNullOrWhiteSpace(data.FeeNameOverseas)) result.Add(new SubjectAnomaly(sheetRow, EmptyOverseasName, item));
                var matches = byCode[(data.FeeNo ?? "").Trim()].ToList();
                byte incoming;
                bool numeric = byte.TryParse((data.CustomerType ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out incoming);
                if (matches.Count > 0 && (!numeric || matches.Any(r => !r.CustomerType.HasValue || r.CustomerType.Value != incoming)))
                    result.Add(new SubjectAnomaly(sheetRow, CustomerTypeMismatch, item));
            }
            return result;
        }
        public static void Save(string file, PreviewTable preview, IList<SubjectAnomaly> anomalies)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(file));
            string temporary = Path.Combine(directory, ".finance-report-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                using (var book = new XLWorkbook())
                {
                    var data = book.AddWorksheet("科目数据"); var errors = book.AddWorksheet("异常科目");
                    WriteHeader(data, preview.Headers);
                    WriteHeader(errors, new[] { "异常数据行号", "异常说明" }.Concat(preview.Headers).ToArray());
                    for (int i = 0; i < preview.Records.Count; i++) WriteCells(data, i + 2, 1, preview.Records[i].Cells);
                    for (int i = 0; i < anomalies.Count; i++)
                    {
                        errors.Cell(i + 2, 1).Value = anomalies[i].DataRowNumber;
                        errors.Cell(i + 2, 2).Value = anomalies[i].Description;
                        WriteCells(errors, i + 2, 3, anomalies[i].Record.Cells);
                    }
                    Format(data, preview.Headers.Length); Format(errors, preview.Headers.Length + 2);
                    book.SaveAs(temporary);
                }
                if (File.Exists(file)) File.Replace(temporary, file, null); else File.Move(temporary, file);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void WriteHeader(IXLWorksheet sheet, string[] headers)
        { for (int i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i]; }
        private static void WriteCells(IXLWorksheet sheet, int row, int start, object[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = sheet.Cell(row, start + i); object value = cells[i];
                if (value is int) cell.Value = (int)value;
                else
                {
                    // Literal values preserve leading zeros, manual edits and formula-like text.
                    cell.Style.NumberFormat.Format = "@";
                    cell.Value = value == null ? "" : Convert.ToString(value, CultureInfo.CurrentCulture);
                }
            }
        }
        private static void Format(IXLWorksheet sheet, int count)
        {
            var header = sheet.Range(1, 1, 1, count);
            header.Style.Font.Bold = true; header.Style.Fill.BackgroundColor = XLColor.LightBlue;
            header.Style.Alignment.WrapText = true; sheet.Row(1).Height = 30; sheet.SheetView.FreezeRows(1);
            for (int c = 1; c <= count; c++)
            { sheet.Column(c).AdjustToContents(); sheet.Column(c).Width = Math.Max(14, Math.Min(60, sheet.Column(c).Width + 2)); }
            if (sheet.LastRowUsed().RowNumber() > 1) sheet.RangeUsed().SetAutoFilter();
        }
    }
}
