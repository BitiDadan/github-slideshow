using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using FinanceMappingTool;

static class ExcelImportChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static void Rejected(Action action, string name)
    { bool failed = false; try { action(); } catch (InvalidDataException) { failed = true; } Check(failed, name); }
    public static void Run()
    {
        string dir = Path.Combine(Path.GetTempPath(), "finance-excel-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "template.xlsx"), rules = Path.Combine(dir, "rules.xml");
        File.WriteAllText(rules, "<MappingRules />");
        // Reordered columns: codes are now F/B, while A is a non-code text column.
        var map = new Dictionary<ExcelField, int> {
            {ExcelField.DomesticName,1},{ExcelField.OverseasCode,2},{ExcelField.OverseasName,3},
            {ExcelField.AccountKind,4},{ExcelField.DomesticCode,6}
        };
        try
        {
            using (var book = new XLWorkbook())
            {
                var s = book.AddWorksheet("Demo");
                foreach (var field in ExcelSchema.Fields) s.Cell(2, map[field.Field]).Value = field.Header;
                s.Cell(1,1).Value = "title above header";
                s.Cell(3,1).Value = "国内名称"; s.Cell(3,2).Value = "0009";
                s.Cell(3,3).Value = "Tiền mặt"; s.Cell(3,4).Value = "资产";
                s.Cell(3,6).Value = 12; s.Cell(3,6).Style.NumberFormat.Format = "000000";
                s.Cell(2,7).Value = "unrelated"; s.Cell(3,7).Value = XLError.DivisionByZero;
                book.SaveAs(file);
            }
            using (var session = MappingEngine.Open(file, "Demo", 2))
            {
                Check(session.AutomaticMapping.Count == 5 && session.AutomaticMapping[ExcelField.DomesticCode] == 6, "reordered headers auto-match by name");
                var row = session.Read(session.AutomaticMapping, rules).Rows.Single();
                Check(row.ExcelRow == 3 && row.FeeNo == "000012" && row.FeeNoOverseas == "0009" && row.CustomerType == "1", "mapped columns, header row and leading zero formats used during data read");
                Check(session.Columns.Single(c=>c.Number==5).Label.Contains("空表头"), "blank headers remain selectable by column letter");
            }
            using (var book = new XLWorkbook(file))
            { book.Worksheet("Demo").Cell(2,6).Value = "本地账户号"; book.Save(); }
            using (var session = MappingEngine.Open(file, "Demo", 2))
            {
                Check(session.AutomaticMapping.Count == 4, "renamed header requires manual mapping");
                Rejected(()=>session.Read(session.AutomaticMapping, rules), "incomplete mapping blocks import");
                Check(session.Read(map, rules).Rows.Single().FeeNo == "000012", "manual mapping reads renamed source column");
                var duplicate = new Dictionary<ExcelField,int>(map); duplicate[ExcelField.DomesticName] = 2;
                Rejected(()=>session.Read(duplicate, rules), "same source cannot serve multiple required fields");
                var invalid = new Dictionary<ExcelField,int>(map); invalid[ExcelField.DomesticName] = 999;
                Rejected(()=>session.Read(invalid, rules), "nonexistent source column rejected");
                // Change the file after opening: the dialog/session continues using its original in-memory snapshot.
                using (var changed = new XLWorkbook(file)) { changed.Worksheet("Demo").Cell(3,6).Value = 999; changed.Save(); }
                Check(session.Read(map, rules).Rows.Single().FeeNo == "000012", "mapping and read use same workbook snapshot");
            }
            using (var book = new XLWorkbook(file))
            {
                var s = book.Worksheet("Demo"); s.Cell(2,6).Value = "科目编码"; s.Cell(2,5).Value = "科目编码";
                s.Cell(3,6).Value = 12.5; book.Save();
            }
            using (var session = MappingEngine.Open(file, "Demo", 2))
            {
                Check(session.AutomaticMapping.Count == 4 && !session.AutomaticMapping.ContainsKey(ExcelField.DomesticCode), "duplicate header is ambiguous and never picks first silently");
                Rejected(()=>session.Read(map, rules), "numeric code validation follows selected role rather than original A/D positions");
            }
            using (var book = new XLWorkbook(file))
            { var s = book.Worksheet("Demo"); s.Cell(3,6).Value = 12; s.Cell(3,1).Value = 12.5; book.Save(); }
            using (var session = MappingEngine.Open(file, "Demo", 2))
                Check(session.Read(map, rules).Rows.Count==1, "non-code source in original A column does not inherit code validation");
            Rejected(()=>MappingEngine.Open(file,"Missing",2), "missing worksheet produces explicit error");
        }
        finally { Directory.Delete(dir, true); }
    }
}
