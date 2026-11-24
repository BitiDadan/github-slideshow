using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
namespace FinanceMappingTool
{
    public static class CustomerReferenceReader
    {
        public const string FileName = "当前主数据.xlsx";
        public const string WorksheetName = "Main";
        public static string GetFilePath(string selectedExcelFile)
        {
            if (string.IsNullOrWhiteSpace(selectedExcelFile)) throw new InvalidDataException("请先选择需要解析的Excel文件。");
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(selectedExcelFile)), FileName);
        }
        public static List<CustomerReference> Read(string file)
        {
            if (!File.Exists(file)) throw new FileNotFoundException("未找到参照文件，请将“当前主数据.xlsx”放在所选择Excel文件的同目录下：" + file, file);
            using (var book = new XLWorkbook(file))
            {
                IXLWorksheet sheet;
                if (!book.TryGetWorksheet(WorksheetName, out sheet)) throw new InvalidDataException("参照文件缺少工作表“Main”：" + file);
                int headerRow = sheet.FirstRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 1;
                int lastColumn = sheet.Row(headerRow).LastCellUsed(XLCellsUsedOptions.Contents)?.Address.ColumnNumber ?? 0;
                var headers = Enumerable.Range(1, lastColumn).ToDictionary(c => c, c => MappingEngine.Cell(sheet, headerRow, c));
                int code = Find(headers, "FeeNo", true), customer = Find(headers, "CustomerType", true);
                int overseas = Find(headers, "OverseasType", false), deleted = Find(headers, "IsDeleted", false);
                int lastRow = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? headerRow;
                var result = new List<CustomerReference>();
                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    if (overseas > 0 && !string.Equals(MappingEngine.Cell(sheet, r, overseas), "KDVoucher", StringComparison.OrdinalIgnoreCase)) continue;
                    if (deleted > 0 && ValueText(sheet, r, deleted) != "0") continue;
                    string number = MappingEngine.Cell(sheet, r, code, true);
                    if (number.Length == 0) continue;
                    string value = ValueText(sheet, r, customer);
                    byte type;
                    if (value.Length > 0 && (!byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out type) || (type != 1 && type != 2)))
                        throw new InvalidDataException("参照文件Main第" + r + "行的CustomerType必须为1（客户）、2（供应商）或空值。");
                    result.Add(new CustomerReference { FeeNo = number, CustomerType = value.Length == 0 ? (byte?)null : byte.Parse(value, CultureInfo.InvariantCulture) });
                }
                return result;
            }
        }
        private static string ValueText(IXLWorksheet sheet, int row, int column)
        {
            var cell = sheet.Cell(row, column);
            return cell.DataType == XLDataType.Number ? cell.GetDouble().ToString("R", CultureInfo.InvariantCulture) : MappingEngine.Cell(sheet, row, column);
        }
        private static int Find(IDictionary<int, string> headers, string name, bool required)
        {
            var matches = headers.Where(h => string.Equals(h.Value.Trim(), name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Key).ToList();
            if (matches.Count > 1) throw new InvalidDataException("参照文件Main的列名“" + name + "”重复，无法确定来源列。");
            if (matches.Count == 0 && required) throw new InvalidDataException("参照文件Main缺少列“" + name + "”；请在首个非空行提供列头。");
            return matches.Count == 0 ? 0 : matches[0];
        }
    }
}
