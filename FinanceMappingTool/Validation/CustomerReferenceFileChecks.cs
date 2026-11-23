using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using FinanceMappingTool;
static class CustomerReferenceFileChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static void Invalid(Action action, string name) { bool failed = false;try { action(); } catch(InvalidDataException){failed=true;}Check(failed,name); }
    public static void Run()
    {
        string dir=Path.Combine(Path.GetTempPath(),"finance-reference-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        string file=Path.Combine(dir,"当前主数据.xlsx");
        try
        {
            Check(CustomerReferenceReader.DefaultFilePath==Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"当前主数据.xlsx"),"reference path uses EXE directory, not working directory");
            bool missing=false;try{CustomerReferenceReader.Read(file);}catch(FileNotFoundException){missing=true;}Check(missing,"missing source file gives explicit error");
            using(var book=new XLWorkbook())
            {
                var s=book.AddWorksheet("Main");s.Cell(3,1).Value=" CustomerType ";s.Cell(3,3).Value="feeno";
                s.Cell(4,1).Value=1;s.Cell(4,1).Style.NumberFormat.Format="0.00";s.Cell(4,3).Value="0001";
                s.Cell(5,1).Value="2";s.Cell(5,3).Value=7;s.Cell(5,3).Style.NumberFormat.Format="000000";
                s.Cell(6,3).Value="0008";book.SaveAs(file);
            }
            var data=CustomerReferenceReader.Read(file);
            Check(data.Count==3 && data[0].FeeNo=="0001" && data[0].CustomerType==1,"reference reads Main by header names regardless of column order, casing, whitespace and blank leading rows");
            Check(data[1].FeeNo=="000007" && data[1].CustomerType==2 && data[2].CustomerType==null,"code formatting and numeric/text/empty customer values retained");
            using(var book=new XLWorkbook(file))
            {
                var s=book.Worksheet("Main");s.Cell(3,4).Value="OverseasType";s.Cell(3,5).Value="IsDeleted";
                s.Cell(4,4).Value="KDVoucher";s.Cell(4,5).Value=0;s.Cell(4,5).Style.NumberFormat.Format="0.00";
                s.Cell(5,4).Value="OtherType";s.Cell(5,5).Value=0;
                s.Cell(6,4).Value="KDVoucher";s.Cell(6,5).Value=1;book.Save();
            }
            Check(CustomerReferenceReader.Read(file).Count==1,"optional OverseasType and IsDeleted columns filter active KDVoucher rows");
            using(var book=new XLWorkbook(file)){book.Worksheet("Main").Cell(4,1).Value="invalid";book.Save();}
            Invalid(()=>CustomerReferenceReader.Read(file),"invalid CustomerType blocks reference load");
            using(var book=new XLWorkbook(file)){book.Worksheet("Main").Cell(3,2).Value="FeeNo";book.Save();}
            Invalid(()=>CustomerReferenceReader.Read(file),"duplicate reference headers are rejected");
            using(var book=new XLWorkbook()){book.AddWorksheet("Main").Cell(1,1).Value="FeeNo";book.SaveAs(file);}
            Invalid(()=>CustomerReferenceReader.Read(file),"required CustomerType header is checked");
            using(var book=new XLWorkbook()){book.AddWorksheet("Other");book.SaveAs(file);}
            Invalid(()=>CustomerReferenceReader.Read(file),"missing Main worksheet is checked");
        }
        finally{Directory.Delete(dir,true);}
    }
}
