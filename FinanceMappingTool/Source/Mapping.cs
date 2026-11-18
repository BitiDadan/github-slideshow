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
        internal static string Cell(IXLWorksheet sheet, int row, int col, bool isCode = false)
        {
            var c = sheet.Cell(row, col);
            if (c.DataType == XLDataType.Error)
                throw new InvalidDataException("Excel 单元格 " + c.Address + " 包含错误值。");
            if (isCode && c.DataType == XLDataType.Number)
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
            using (var session = Open(file, sheetName, headerRow))
            {
                var map = session.AutomaticMapping;
                ExcelSchema.Validate(session.Columns, map);
                return session.Read(map, ruleFile);
            }
        }

        public static ExcelImportSession Open(string file, string sheetName, int headerRow)
        { return new ExcelImportSession(file, sheetName, headerRow); }

        internal static ImportResult ReadSheet(IXLWorksheet sheet, int headerRow, IDictionary<ExcelField, int> map, string ruleFile)
        {
            var result = new ImportResult();
            var rules = XDocument.Load(ruleFile).Root;
            if (rules == null || rules.Name != "MappingRules") throw new InvalidDataException("规则文件根节点错误。");
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int last = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? headerRow;
            for (int r = headerRow + 1; r <= last; r++)
            {
                string foreign = Cell(sheet, r, map[ExcelField.OverseasCode], true), domestic = Cell(sheet, r, map[ExcelField.DomesticCode], true);
                if (foreign.Length == 0 && domestic.Length == 0) continue;
                if (foreign.Length == 0 || domestic.Length == 0)
                {
                    result.Skipped++;
                    result.Messages.Add("第 " + r + " 行：一侧科目编码为空，已跳过（国内=" + domestic + "，海外=" + foreign + "）。");
                    continue;
                }
                var item = new MappingRow { ExcelRow = r, FeeNo = domestic,
                    FeeName = Cell(sheet, r, map[ExcelField.DomesticName]), FeeNoOverseas = foreign,
                    FeeNameOverseas = Cell(sheet, r, map[ExcelField.OverseasName]), OverseasType = "KDVoucher",
                    CustomerType = Cell(sheet, r, map[ExcelField.AccountKind]) == "资产" ? "1" : "2",
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
            Validate(result.Rows);
            return result;
        }

        public static List<MappingRow> ForType(IEnumerable<MappingRow> source, string type, string ruleFile)
        {
            type = (type ?? "").Trim();
            Check(type, 50, true, true, "映射类型");
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
            const string columns = "[FeeNo],[FeeName],[FeeNoOverseas],[FeeNameOverseas],[OverseasType],[Memo],[CustomerType],[CreditDebitType]";
            const string definition = "[FeeNo] varchar(20),[FeeName] nvarchar(400),[FeeNoOverseas] varchar(20),[FeeNameOverseas] nvarchar(400),[OverseasType] varchar(50),[Memo] nvarchar(400),[CustomerType] tinyint,[CreditDebitType] tinyint";
            s.AppendLine("-- 财务科目映射导入 v1.2.0；仅 INSERT，不包含后期 UPDATE。");
            s.AppendLine("-- 批量读取目标映射一次；相同内容跳过，冲突或重复则回滚。默认锁等待10秒；已有有限会话超时则沿用。");
            s.AppendLine("-- 手动取消后，请在原窗口检查 SELECT @@SPID, @@TRANCOUNT, XACT_STATE(); 注意客户端取消不一定进入 CATCH。");
            s.AppendLine("USE [" + database.Replace("]", "]]") + "];\r\nGO\r\nSET NOCOUNT ON;");
            s.AppendLine("IF @@TRANCOUNT <> 0\r\n    THROW 50003, N'当前窗口存在未结束事务。请先确认并提交或回滚原事务，再执行导入。', 1;");
            s.AppendLine("DECLARE @previousLockTimeout int = @@LOCK_TIMEOUT;");
            s.AppendLine("DECLARE @previousXactAbort bit = CASE WHEN (@@OPTIONS & 16384) = 16384 THEN 1 ELSE 0 END;");
            s.AppendLine("BEGIN TRY\r\n    IF @previousLockTimeout=-1 SET LOCK_TIMEOUT 10000;\r\n    SET XACT_ABORT ON;");
            s.AppendLine("    DECLARE @Import TABLE ([ExcelRow] int," + definition + ", PRIMARY KEY ([OverseasType],[FeeNo]));");
            // SQL Server limits a direct INSERT VALUES constructor to 1000 rows.
            for (int offset = 0; offset < rows.Count; offset += 1000)
            {
                s.AppendLine("    INSERT INTO @Import ([ExcelRow]," + columns + ") VALUES");
                int end = Math.Min(offset + 1000, rows.Count);
                for (int i = offset; i < end; i++)
                {
                    var r = rows[i];
                    var values = new[] { r.ExcelRow.ToString(CultureInfo.InvariantCulture), Q(r.FeeNo, false), Q(r.FeeName),
                        Q(r.FeeNoOverseas, false), Q(r.FeeNameOverseas), Q(r.OverseasType, false), Q(r.Memo), r.CustomerType,
                        string.IsNullOrWhiteSpace(r.CreditDebitType) ? "NULL" : byte.Parse(r.CreditDebitType).ToString(CultureInfo.InvariantCulture) };
                    s.AppendLine("        (" + string.Join(",", values) + ")" + (i + 1 == end ? ";" : ","));
                }
            }
            s.AppendLine("    DECLARE @expected int = (SELECT COUNT(*) FROM @Import), @inserted int, @sessionId int = @@SPID;");
            s.AppendLine("    DECLARE @effectiveLockTimeout int = @@LOCK_TIMEOUT;\r\n    RAISERROR(N'导入会话 SPID=%d，数据%d条；正在查询并锁定目标映射，锁等待超时设置%d毫秒。', 10, 1, @sessionId, @expected, @effectiveLockTimeout) WITH NOWAIT;");
            s.AppendLine("    DECLARE @Existing TABLE (" + definition + ");");
            s.AppendLine("    BEGIN TRANSACTION;");
            s.AppendLine("    INSERT INTO @Existing (" + columns + ")");
            s.AppendLine("    SELECT " + string.Join(",", columns.Split(',').Select(c => "t." + c)));
            s.AppendLine("    FROM [dbo].[VoucherFeeRelation] AS t WITH (UPDLOCK,HOLDLOCK)");
            s.AppendLine("    INNER JOIN @Import AS i ON i.[OverseasType]=t.[OverseasType] AND i.[FeeNo]=t.[FeeNo]");
            s.AppendLine("    WHERE t.[IsDeleted]=0 OPTION (RECOMPILE);");
            s.AppendLine("    IF EXISTS (SELECT 1 FROM @Existing GROUP BY [OverseasType],[FeeNo] HAVING COUNT_BIG(*)>1)");
            s.AppendLine("    BEGIN\r\n        SELECT [OverseasType],[FeeNo],COUNT_BIG(*) AS [重复有效记录数] FROM @Existing GROUP BY [OverseasType],[FeeNo] HAVING COUNT_BIG(*)>1;");
            s.AppendLine("        THROW 50001, N'目标库存在重复有效映射，详情见结果集。导入已回滚。', 1;\r\n    END;");
            string diff = string.Join(" OR ", new[] { "FeeName", "FeeNoOverseas", "FeeNameOverseas", "Memo" }.Select(c =>
                "e.[" + c + "] IS NULL OR CONVERT(varbinary(max),e.[" + c + "])<>CONVERT(varbinary(max),i.[" + c + "])"));
            diff += " OR e.[CustomerType] IS NULL OR e.[CustomerType]<>i.[CustomerType]";
            diff += " OR e.[CreditDebitType]<>i.[CreditDebitType] OR (e.[CreditDebitType] IS NULL AND i.[CreditDebitType] IS NOT NULL) OR (e.[CreditDebitType] IS NOT NULL AND i.[CreditDebitType] IS NULL)";
            string conflict = "FROM @Import AS i INNER JOIN @Existing AS e ON e.[OverseasType]=i.[OverseasType] AND e.[FeeNo]=i.[FeeNo] WHERE " + diff;
            s.AppendLine("    IF EXISTS (SELECT 1 " + conflict + ")\r\n    BEGIN");
            s.AppendLine("        SELECT N'映射内容冲突：i为导入内容，e为已有内容' AS [问题],i.*,e.* " + conflict + ";");
            s.AppendLine("        THROW 50002, N'目标库映射内容冲突，详情见结果集。导入已回滚。', 1;\r\n    END;");
            s.AppendLine("    RAISERROR(N'目标映射检查完成，正在批量插入。', 10, 1) WITH NOWAIT;");
            s.AppendLine("    INSERT INTO [dbo].[VoucherFeeRelation] (" + columns + ")");
            s.AppendLine("    SELECT " + string.Join(",", columns.Split(',').Select(c => "i." + c)) + " FROM @Import AS i");
            s.AppendLine("    WHERE NOT EXISTS (SELECT 1 FROM @Existing AS e WHERE e.[OverseasType]=i.[OverseasType] AND e.[FeeNo]=i.[FeeNo]) OPTION (RECOMPILE);");
            s.AppendLine("    SET @inserted = @@ROWCOUNT;\r\n    COMMIT TRANSACTION;");
            s.AppendLine("    IF @previousLockTimeout=-1 SET LOCK_TIMEOUT -1;\r\n    IF @previousXactAbort=0 SET XACT_ABORT OFF;");
            s.AppendLine("    SELECT @inserted AS [本次新增条数],@expected-@inserted AS [已存在跳过条数],@expected AS [本次数据条数];");
            s.AppendLine("END TRY\r\nBEGIN CATCH\r\n    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;");
            s.AppendLine("    IF @previousLockTimeout=-1 SET LOCK_TIMEOUT -1;\r\n    IF @previousXactAbort=0 SET XACT_ABORT OFF;");
            s.AppendLine("    IF ERROR_NUMBER()=1222\r\n        RAISERROR(N'等待数据库锁超时，已回滚。请记录上方SPID，在另一窗口查询阻塞会话；不要反复重试或使用NOLOCK绕过检查。', 10, 1) WITH NOWAIT;");
            s.AppendLine("    THROW;\r\nEND CATCH;\r\nGO");
            return s.ToString();
        }
    }
}
