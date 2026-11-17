using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace FinanceMappingTool
{
    public sealed class MappingRow
    {
        public int ExcelRow { get; set; }
        public string FeeNo { get; set; }
        public string FeeName { get; set; }
        public string FeeNoOverseas { get; set; }
        public string FeeNameOverseas { get; set; }
        public string OverseasType { get; set; }
        public string CustomerType { get; set; }
        public string CreditDebitType { get; set; }
        public string Memo { get; set; }
        public MappingRow Copy() { return (MappingRow)MemberwiseClone(); }
    }

    public sealed class ImportResult
    {
        public List<MappingRow> Rows { get; } = new List<MappingRow>();
        public List<string> Messages { get; } = new List<string>();
        public int Skipped { get; set; }
        public int Duplicates { get; set; }
    }

    public static class MappingEngine
    {
        private static string Cell(IXLWorksheet sheet, int row, int col)
        {
            var c = sheet.Cell(row, col);
            if (c.DataType == XLDataType.Error)
                throw new InvalidDataException("Excel 单元格 " + c.Address + " 包含错误值。");
            if ((col == 1 || col == 4) && c.DataType == XLDataType.Number)
            {
                double number = c.GetDouble();
                if (number < 0 || number != Math.Truncate(number))
                    throw new InvalidDataException("科目编码单元格 " + c.Address + " 不是非负整数，请设为文本后检查。");
                if (string.IsNullOrEmpty(c.Style.NumberFormat.Format) ||
                    string.Equals(c.Style.NumberFormat.Format, "General", StringComparison.OrdinalIgnoreCase))
                    return number.ToString("0", CultureInfo.InvariantCulture);
            }
            return c.GetFormattedString().Trim();
        }

        public static ImportResult Read(string file, string sheetName, int headerRow, string ruleFile)
        {
            var result = new ImportResult();
            var rules = XDocument.Load(ruleFile).Root;
            if (rules == null || rules.Name != "MappingRules") throw new InvalidDataException("规则文件根节点错误。");
            using (var book = new XLWorkbook(file))
            {
                IXLWorksheet sheet;
                if (!book.TryGetWorksheet(sheetName, out sheet))
                    throw new InvalidDataException("找不到工作表：" + sheetName);
                var expected = new Dictionary<int, string> {
                    {1,"*编码"}, {3,"名称（越南文）"}, {4,"科目编码"}, {5,"科目名称"}, {6,"科目类型"}
                };
                foreach (var pair in expected)
                    if (Cell(sheet, headerRow, pair.Key) != pair.Value)
                        throw new InvalidDataException("表头不匹配：" + sheet.Cell(headerRow, pair.Key).Address +
                            " 应为“" + pair.Value + "”。请检查工作表和表头行。");
                var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int last = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? headerRow;
                for (int r = headerRow + 1; r <= last; r++)
                {
                    string foreign = Cell(sheet, r, 1), domestic = Cell(sheet, r, 4);
                    if (foreign.Length == 0 && domestic.Length == 0) continue;
                    if (foreign.Length == 0 || domestic.Length == 0)
                    {
                        result.Skipped++;
                        result.Messages.Add("第 " + r + " 行：一侧科目编码为空，已跳过（国内=" + domestic + "，海外=" + foreign + "）。");
                        continue;
                    }
                    var item = new MappingRow { ExcelRow = r, FeeNo = domestic,
                        FeeName = Cell(sheet, r, 5), FeeNoOverseas = foreign,
                        FeeNameOverseas = Cell(sheet, r, 3), OverseasType = "KDVoucher",
                        CustomerType = Cell(sheet, r, 6) == "资产" ? "1" : "2",
                        CreditDebitType = "", Memo = "" };
                    foreach (var rule in rules.Elements("Override"))
                    {
                        if ((string)rule.Attribute("enabled") != "true" ||
                            (string)rule.Attribute("sourceFeeNo") != domestic ||
                            (string)rule.Attribute("sourceOverseas") != foreign) continue;
                        item.FeeNo = (string)rule.Attribute("feeNo") ?? item.FeeNo;
                        item.FeeNoOverseas = (string)rule.Attribute("overseas") ?? item.FeeNoOverseas;
                        item.FeeName = (string)rule.Attribute("feeName") ?? item.FeeName;
                        item.FeeNameOverseas = (string)rule.Attribute("overseasName") ?? item.FeeNameOverseas;
                        item.CustomerType = (string)rule.Attribute("customerType") ?? item.CustomerType;
                        item.CreditDebitType = (string)rule.Attribute("creditDebitType") ?? item.CreditDebitType;
                        result.Messages.Add("第 " + r + " 行：已应用配置中的人工修订。");
                    }
                    if (seen.ContainsKey(item.FeeNo))
                    {
                        result.Duplicates++;
                        result.Messages.Add("第 " + r + " 行：国内科目 " + item.FeeNo + " 重复，保留第 " + seen[item.FeeNo] +
                            " 行，忽略本行海外映射 " + item.FeeNoOverseas + "。");
                        continue;
                    }
                    seen.Add(item.FeeNo, r);
                    if (item.FeeName.Length == 0 || item.FeeNameOverseas.Length == 0)
                        result.Messages.Add("第 " + r + " 行：名称为空，仍保留该映射，请预览确认。");
                    result.Rows.Add(item);
                }
            }
            Validate(result.Rows);
            return result;
        }

        public static List<MappingRow> ForType(IEnumerable<MappingRow> source, string type, string ruleFile)
        {
            if (type != "KDVoucher" && type != "KDVoucher02") throw new InvalidDataException("不支持的映射类型。");
            var root = XDocument.Load(ruleFile).Root;
            var rows = source.Select(x => x.Copy()).ToList();
            foreach (var row in rows)
            {
                row.OverseasType = type;
                if (type == "KDVoucher02")
                {
                    row.FeeNoOverseas = row.FeeNo;
                    row.FeeNameOverseas = row.FeeName;
                    row.Memo = "02账套";
                    foreach (var rule in root.Elements("Type02Override"))
                    {
                        if ((string)rule.Attribute("feeNo") != row.FeeNo) continue;
                        row.FeeNoOverseas = (string)rule.Attribute("overseas") ?? row.FeeNoOverseas;
                        row.FeeNameOverseas = (string)rule.Attribute("overseasName") ?? row.FeeNameOverseas;
                    }
                }
            }
            Validate(rows);
            return rows;
        }

        public static void Validate(IList<MappingRow> rows)
        {
            if (rows.Count == 0) throw new InvalidDataException("没有可导出的有效映射。");
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
            {
                string at = "Excel 第 " + r.ExcelRow + " 行：";
                Check(r.FeeNo, 20, true, true, at + "国内编码");
                Check(r.FeeNoOverseas, 20, true, true, at + "海外编码");
                Check(r.OverseasType, 50, true, true, at + "映射类型");
                Check(r.FeeName, 400, false, false, at + "国内名称");
                Check(r.FeeNameOverseas, 400, false, false, at + "海外名称");
                Check(r.Memo, 400, false, false, at + "备注");
                if (r.CustomerType != "1" && r.CustomerType != "2")
                    throw new InvalidDataException(at + "CustomerType 只能为 1 或 2。");
                byte credit;
                if (!string.IsNullOrWhiteSpace(r.CreditDebitType) &&
                    !byte.TryParse(r.CreditDebitType, NumberStyles.None, CultureInfo.InvariantCulture, out credit))
                    throw new InvalidDataException(at + "CreditDebitType 必须为空或 0～255 的整数。");
                if (!keys.Add(r.OverseasType + "\0" + r.FeeNo))
                    throw new InvalidDataException(at + "同一映射类型下国内编码重复：" + r.FeeNo);
            }
        }

        private static void Check(string value, int limit, bool required, bool ascii, string field)
        {
            if (required && string.IsNullOrWhiteSpace(value)) throw new InvalidDataException(field + "不能为空。");
            if ((value ?? "").Length > limit) throw new InvalidDataException(field + "超过长度 " + limit + "。");
            if ((value ?? "").Any(c => c == '\0' || (ascii && (c < 32 || c > 126))))
                throw new InvalidDataException(field + "包含控制字符或非 ASCII 编码字符。");
        }

        private static string Q(string s, bool unicode = true)
        { return (unicode ? "N" : "") + "'" + (s ?? "").Replace("'", "''") + "'"; }

        public static string Sql(IList<MappingRow> rows, string database)
        {
            Validate(rows);
            if (string.IsNullOrWhiteSpace(database) || database.Length > 128 || database.Any(char.IsControl))
                throw new InvalidDataException("请填写有效的目标数据库名称（最多128字符）。");
            var s = new StringBuilder();
            s.AppendLine("-- 财务科目映射导入；仅 INSERT，不包含后期 UPDATE。");
            s.AppendLine("-- 已存在且内容相同的有效记录跳过；同类型同国内编码内容不同时终止并回滚。");
            s.AppendLine("-- 执行前请确认目标库、预览映射并完成常规备份。记录数：" + rows.Count);
            s.AppendLine("USE [" + database.Replace("]", "]]") + "];\r\nGO\r\nSET NOCOUNT ON;\r\nSET XACT_ABORT ON;");
            s.AppendLine("BEGIN TRY\r\n    BEGIN TRANSACTION;");
            s.AppendLine("    DECLARE @inserted int = 0;");
            foreach (var r in rows)
            {
                var values = new[] { Q(r.FeeNo, false), Q(r.FeeName), Q(r.FeeNoOverseas, false),
                    Q(r.FeeNameOverseas), Q(r.OverseasType, false), Q(r.Memo), r.CustomerType,
                    string.IsNullOrWhiteSpace(r.CreditDebitType) ? "NULL" : byte.Parse(r.CreditDebitType).ToString(CultureInfo.InvariantCulture) };
                string key = "[OverseasType]=" + values[4] + " AND [FeeNo]=" + values[0] + " AND [IsDeleted]=0";
                // Byte comparisons detect trailing spaces/case differences and avoid collation-dependent equality.
                string diff = string.Join(" OR ", new[] {1,2,3,5}.Select(i => {
                    string col = new[] {"FeeNo","FeeName","FeeNoOverseas","FeeNameOverseas","OverseasType","Memo"}[i];
                    return "[" + col + "] IS NULL OR CONVERT(varbinary(max),[" + col + "])<>CONVERT(varbinary(max)," + values[i] + ")";
                }));
                diff += " OR [CustomerType] IS NULL OR [CustomerType]<>" + values[6];
                diff += values[7] == "NULL" ? " OR [CreditDebitType] IS NOT NULL" : " OR [CreditDebitType] IS NULL OR [CreditDebitType]<>" + values[7];
                s.AppendLine("    -- Excel 行 " + r.ExcelRow);
                s.AppendLine("    IF (SELECT COUNT_BIG(*) FROM [dbo].[VoucherFeeRelation] WITH (UPDLOCK,HOLDLOCK) WHERE " + key + ") > 1");
                s.AppendLine("        THROW 50001, " + Q("目标库存在重复有效映射：" + r.OverseasType + "/" + r.FeeNo) + ", 1;");
                s.AppendLine("    IF EXISTS (SELECT 1 FROM [dbo].[VoucherFeeRelation] WITH (UPDLOCK,HOLDLOCK) WHERE " + key + " AND (" + diff + "))");
                s.AppendLine("        THROW 50002, " + Q("目标库映射内容冲突：" + r.OverseasType + "/" + r.FeeNo + "；请核实后处理。") + ", 1;");
                s.AppendLine("    IF NOT EXISTS (SELECT 1 FROM [dbo].[VoucherFeeRelation] WITH (UPDLOCK,HOLDLOCK) WHERE " + key + ")\r\n    BEGIN");
                s.AppendLine("        INSERT INTO [dbo].[VoucherFeeRelation] ([FeeNo],[FeeName],[FeeNoOverseas],[FeeNameOverseas],[OverseasType],[Memo],[CustomerType],[CreditDebitType])");
                s.AppendLine("        VALUES (" + string.Join(",", values) + ");\r\n        SET @inserted += 1;\r\n    END;");
            }
            s.AppendLine("    COMMIT TRANSACTION;\r\n    SELECT @inserted AS [本次新增条数], " + rows.Count + " - @inserted AS [已存在跳过条数];");
            s.AppendLine("END TRY\r\nBEGIN CATCH\r\n    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;\r\n    THROW;\r\nEND CATCH;\r\nGO");
            return s.ToString();
        }
    }
}
