using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace FinanceMappingTool
{
    public enum ExcelField { OverseasCode, OverseasName, DomesticCode, DomesticName, AccountKind }

    public sealed class RequiredExcelField
    {
        public ExcelField Field { get; }
        public string Header { get; }
        public string Description { get; }
        public RequiredExcelField(ExcelField field, string header, string description)
        { Field = field; Header = header; Description = description; }
    }

    public sealed class ExcelColumn
    {
        public int Number { get; }
        public string Letter { get; }
        public string Header { get; }
        public string Sample { get; }
        public string Label => Letter + "列 | " + (Header.Length == 0 ? "（空表头）" : Header);
        public ExcelColumn(int number, string letter, string header, string sample)
        { Number = number; Letter = letter; Header = header; Sample = sample; }
        public override string ToString() { return Label; }
    }

    public static class ExcelSchema
    {
        public static IReadOnlyList<RequiredExcelField> Fields { get; } = new[] {
            new RequiredExcelField(ExcelField.OverseasCode, "*编码", "海外科目编码 / FeeNoOverseas"),
            new RequiredExcelField(ExcelField.OverseasName, "名称（越南文）", "海外科目名称 / FeeNameOverseas"),
            new RequiredExcelField(ExcelField.DomesticCode, "科目编码", "国内科目编码 / FeeNo"),
            new RequiredExcelField(ExcelField.DomesticName, "科目名称", "国内科目名称 / FeeName"),
            new RequiredExcelField(ExcelField.AccountKind, "科目类型", "用于判定客户/供应商属性")
        };

        public static Dictionary<ExcelField, int> Match(IReadOnlyList<ExcelColumn> columns)
        {
            var map = new Dictionary<ExcelField, int>();
            foreach (var field in Fields)
            {
                var matches = columns.Where(c => string.Equals(c.Header.Trim(), field.Header, StringComparison.Ordinal)).ToList();
                // A repeated header is ambiguous: require an explicit choice instead of using its first occurrence.
                if (matches.Count == 1) map.Add(field.Field, matches[0].Number);
            }
            return map;
        }

        public static void Validate(IReadOnlyList<ExcelColumn> columns, IDictionary<ExcelField, int> map)
        {
            if (map == null) throw new InvalidDataException("请先选择Excel字段对应关系。");
            var used = new HashSet<int>();
            foreach (var field in Fields)
            {
                int number;
                if (!map.TryGetValue(field.Field, out number) || !columns.Any(c => c.Number == number))
                    throw new InvalidDataException("请为“" + field.Header + "”（" + field.Description + "）选择来源列。");
                if (!used.Add(number)) throw new InvalidDataException("同一个Excel来源列不能同时对应多个所需字段。");
            }
        }
    }

    // One workbook per import: the dialog and data read use the same in-memory file snapshot.
    public sealed class ExcelImportSession : IDisposable
    {
        private readonly XLWorkbook book;
        internal IXLWorksheet Sheet { get; }
        public int HeaderRow { get; }
        public IReadOnlyList<ExcelColumn> Columns { get; }
        public Dictionary<ExcelField, int> AutomaticMapping => ExcelSchema.Match(Columns);

        public ExcelImportSession(string file, string sheetName, int headerRow)
        {
            if (headerRow < 1 || headerRow > 1048576) throw new InvalidDataException("表头行超出Excel有效范围。");
            book = new XLWorkbook(file);
            try
            {
                IXLWorksheet worksheet;
                if (!book.TryGetWorksheet(sheetName, out worksheet)) throw new InvalidDataException("找不到工作表：" + sheetName);
                Sheet = worksheet; HeaderRow = headerRow;
                int lastColumn = Sheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0;
                if (lastColumn == 0) throw new InvalidDataException("工作表为空，没有可选择的列。");
                int lastRow = Sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? headerRow;
                var columns = new List<ExcelColumn>();
                for (int c = 1; c <= lastColumn; c++)
                {
                    var samples = new List<string>();
                    for (int r = headerRow + 1; r <= Math.Min(lastRow, headerRow + 5); r++)
                    {
                        string value = PreviewCell(Sheet, r, c);
                        if (value.Length > 0) samples.Add(value.Replace("\r", " ").Replace("\n", " "));
                    }
                    columns.Add(new ExcelColumn(c, Sheet.Cell(headerRow, c).Address.ColumnLetter,
                        PreviewCell(Sheet, headerRow, c), string.Join("；", samples.Take(3))));
                }
                Columns = columns.AsReadOnly();
            }
            catch { book.Dispose(); throw; }
        }

        private static string PreviewCell(IXLWorksheet sheet, int row, int column)
        {
            // An unrelated formula/error must not prevent selecting the five actual source columns.
            try { return MappingEngine.Cell(sheet, row, column); }
            catch (Exception) { return "（无法预览）"; }
        }

        public ImportResult Read(IDictionary<ExcelField, int> mapping, string ruleFile)
        {
            ExcelSchema.Validate(Columns, mapping);
            // Snapshot the selection before reading any data.
            return MappingEngine.ReadSheet(Sheet, HeaderRow, new Dictionary<ExcelField, int>(mapping), ruleFile);
        }
        public void Dispose() { book.Dispose(); }
    }
}
