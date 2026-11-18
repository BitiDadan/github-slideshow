using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using FinanceMappingTool;
using Microsoft.SqlServer.TransactSql.ScriptDom;
class SqlGenerationChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Parse(string sql, string name)
    {
        IList<ParseError> errors;
        new TSql120Parser(true).Parse(new StringReader(sql), out errors);
        Check(errors.Count == 0, name + " SQL Server 2014 syntax: " + string.Join("; ", errors.Select(e => e.Message)));
    }
    static void Main(string[] args)
    {
        foreach (int count in new[] { 1, 305, 1001 })
        {
            var rows = Enumerable.Range(1, count).Select(i => new MappingRow {
                ExcelRow = i + 1, FeeNo = i.ToString("000000"), FeeName = "测试'名称",
                FeeNoOverseas = "X" + i, FeeNameOverseas = "Tiền 'mặt'", OverseasType = "DemoType",
                CustomerType = "1", CreditDebitType = "", Memo = ""
            }).ToList();
            string sql = MappingEngine.Sql(rows, "Demo]DB");
            Parse(sql, count + " rows");
            Check(sql.Contains("N'测试''名称'") && sql.Contains("USE [Demo]]DB]"), "Unicode and identifier escaping");
            int batches = sql.Split(new[] { "INSERT INTO @Import" }, StringSplitOptions.None).Length - 1;
            Check(batches == (count + 999) / 1000, "VALUES batch limit");
            Check(sql.Split(new[] { "WITH (UPDLOCK,HOLDLOCK)" }, StringSplitOptions.None).Length == 2, "one target snapshot statement");
            Check(sql.Contains("IF @@TRANCOUNT <> 0") && sql.Contains("SET LOCK_TIMEOUT 10000"), "transaction preflight and bounded lock wait");
            Check(sql.Contains("@expected int = (SELECT COUNT(*) FROM @Import)"), "actual input count");
            // ScriptDom accepts SET LOCK_TIMEOUT variables that the server rejects: explicit regression guard.
            Check(!Regex.IsMatch(sql, @"\bSET\s+LOCK_TIMEOUT\s+@", RegexOptions.IgnoreCase), "LOCK_TIMEOUT must use integer literals, never variables");
            Check(sql.Contains("IF @previousLockTimeout=-1 SET LOCK_TIMEOUT 10000;") && sql.Split(new[] { "IF @previousLockTimeout=-1 SET LOCK_TIMEOUT -1;" }, StringSplitOptions.None).Length == 3, "default timeout restored on both success/error; existing finite timeout left unchanged");
            Check(sql.Contains("IF @previousXactAbort=0 SET XACT_ABORT OFF"), "restore XACT_ABORT");
            Check(sql.Contains("ROLLBACK TRANSACTION") && sql.Contains("THROW 50002"), "conflict rollback retained");
        }
        if (args.Length > 0) Parse(File.ReadAllText(args[0]), "diagnostic script");
        Console.WriteLine("Generation and parser checks only; no SQL Server was executed.");
    }
}
