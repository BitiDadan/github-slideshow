using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using FinanceMappingTool;
static class ExcelReportChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static PreviewRecord Row(string code, string overseasName, string customer, int excelRow)
    {
        var data = new MappingRow { ExcelRow = excelRow, FeeNo = code, FeeName = "手工修改名称", FeeNoOverseas = "Z" + code,
            FeeNameOverseas = overseasName, OverseasType = "CustomType", CustomerType = customer, CreditDebitType = "", Memo = "备注 '中文'" };
        return new PreviewRecord(data, new object[] { excelRow,code,data.FeeName,data.FeeNoOverseas,overseasName,"CustomType",customer,"",data.Memo });
    }
    public static void Run()
    {
        string file = Path.Combine(Path.GetTempPath(), "finance-report-check-" + Guid.NewGuid().ToString("N") + ".xlsx");
        var headers = new[] { "Excel行","国内科目编码","国内科目名称","海外科目编码","海外科目名称","映射类型","客户/供应商","收付属性","备注" };
        var table = new PreviewTable(headers, new[] { Row("0001","","2",20), Row("0002","Good","1",25), Row("0003","  ","2",31),
            Row("0004","Good","1",40),Row("0005","Good","1",50),Row("0006","=1+2","01",60) });
        var baseline = new List<CustomerReference> {
            new CustomerReference {FeeNo="0001",CustomerType=1},new CustomerReference {FeeNo="0002",CustomerType=1},
            new CustomerReference {FeeNo="0004",CustomerType=null},new CustomerReference {FeeNo="0005",CustomerType=1},
            new CustomerReference {FeeNo="0005",CustomerType=2},new CustomerReference {FeeNo="0006",CustomerType=1}
        };
        var issues = ExcelReport.Analyze(table, baseline);
        Check(issues.Count == 5, "empty names and customer mismatches produce expected five anomaly records");
        Check(issues.Count(x=>x.DataRowNumber==2)==2, "both issues on one data row are listed separately");
        Check(issues.All(x=>x.Description=="海外科目名称为空" || x.Description==@"客户\供应商类型异常"), "anomaly labels exactly match requested text");
        Check(!issues.Any(x=>x.DataRowNumber==3 || x.DataRowNumber==7), "same numeric customer type passes despite formatting and independent overseas codes");
        Check(issues.Count(x=>x.DataRowNumber==4)==1, "missing reference code does not create customer mismatch");
        Check(issues.Any(x=>x.DataRowNumber==5 && x.Description==ExcelReport.CustomerTypeMismatch), "NULL reference customer type is reported");
        Check(issues.Any(x=>x.DataRowNumber==6), "conflicting duplicate reference types do not silently choose first");
        Check(ExcelReport.Analyze(table,new List<CustomerReference>()).Count==2, "empty reference only checks empty overseas names");
        bool failed=false;try { ExcelReport.Analyze(table,null); } catch(InvalidDataException){failed=true;}
        Check(failed,"unavailable reference cannot masquerade as completed type check");
        try
        {
            ExcelReport.Save(file,table,issues);
            using(var book=new XLWorkbook(file))
            {
                Check(book.Worksheets.Count==2 && book.Worksheet(1).Name=="科目数据" && book.Worksheet(2).Name=="异常科目", "one workbook always contains exactly two required sheets in order");
                var data=book.Worksheet(1);var errors=book.Worksheet(2);
                Check(data.LastRowUsed().RowNumber()==table.Records.Count+1,"all preview rows exported");
                for(int c=0;c<headers.Length;c++)
                {
                    Check(data.Cell(1,c+1).GetString()==headers[c] && errors.Cell(1,c+3).GetString()==headers[c],"preview header order retained "+c);
                    for(int r=0;r<table.Records.Count;r++)
                        if(data.Cell(r+2,c+1).GetString()!=Convert.ToString(table.Records[r].Cells[c]))throw new Exception("preview value differs");
                }
                Check(errors.Cell(1,1).GetString()=="异常数据行号" && errors.Cell(1,2).GetString()=="异常说明","two anomaly columns added on the left");
                Check(data.Cell(2,2).GetString()=="0001" && data.Cell(2,3).GetString()=="手工修改名称","leading zeros and current manual edits preserved");
                Check(!data.Cell(7,5).HasFormula && data.Cell(7,5).GetString()=="=1+2","formula-like content exported as literal text");
                for(int r=0;r<issues.Count;r++)
                {
                    Check(errors.Cell(r+2,1).GetValue<int>()==issues[r].DataRowNumber && errors.Cell(r+2,2).GetString()==issues[r].Description,"anomaly links to 科目数据 row "+r);
                    for(int c=0;c<headers.Length;c++)
                        if(errors.Cell(r+2,c+3).GetString()!=Convert.ToString(issues[r].Record.Cells[c]))throw new Exception("anomaly data differs from preview");
                }
            }
            ExcelReport.Save(file,table,new List<SubjectAnomaly>());
            using(var book=new XLWorkbook(file))
                Check(book.Worksheets.Count==2 && book.Worksheet("异常科目").LastRowUsed().RowNumber()==1,"zero anomalies still exports second sheet with headers; overwrite works");
            var source=Row("0010","Original","1",100);
            var copied=new PreviewRecord(source.Data,source.Cells);source.Data.FeeNameOverseas="changed";source.Cells[4]="changed";
            Check(copied.Data.FeeNameOverseas=="Original" && (string)copied.Cells[4]=="Original","export snapshot independent from later source edits");
        }
        finally { if(File.Exists(file))File.Delete(file); }
    }
}
