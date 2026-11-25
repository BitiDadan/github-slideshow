using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FinanceMappingTool;
using Microsoft.SqlServer.TransactSql.ScriptDom;
static class LedgerCashChecks
{
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static void Parse(string sql){IList<ParseError> errors;new TSql120Parser(true).Parse(new StringReader(sql),out errors);Check(errors.Count==0,"dual ledger/cash SQL2014 parse "+string.Join(";",errors.Select(e=>e.Message)));}
    static MappingRow Row(int i,string type){return new MappingRow{ExcelRow=i+1,FeeNo=i.ToString("000000"),FeeName="国内名称",FeeNoOverseas="F"+i,FeeNameOverseas="Foreign",OverseasType=type,CustomerType="1",CreditDebitType="",Memo="手工备注"};}
    public static void Run()
    {
        var options=new CompanyExportOptions("KDVoucher","000123","Demo","AUD");
        foreach(int count in new[]{1,305,1001})
        {
            var preview=Enumerable.Range(1,count).Select(i=>Row(i,options.OverseasType)).ToList();
            var expanded=MappingEngine.ForLedgers(preview,options.OverseasType);
            Check(expanded.Count==count*2 && expanded.Take(count).All(r=>r.OverseasType=="KDVoucherAUD") && expanded.Skip(count).All(r=>r.OverseasType=="KDVoucherAUD02"),"two sets of subject rows "+count);
            Check(expanded.Take(count).Select(r=>r.FeeNoOverseas).SequenceEqual(preview.Select(r=>r.FeeNoOverseas)),"primary keeps preview overseas codes");
            Check(expanded.Skip(count).All(r=>r.FeeNoOverseas==r.FeeNo),"secondary overseas code equals its domestic code including leading zeros");
            Check(expanded[count].FeeNameOverseas==preview[0].FeeNameOverseas && expanded[count].Memo==preview[0].Memo && expanded[count].CustomerType==preview[0].CustomerType && !ReferenceEquals(expanded[count],preview[0]),"secondary preserves overseas name and other attributes in a separate copy");
            string sql=MappingEngine.Sql(preview,"DemoDB",options);Parse(sql);
            Check(sql.Contains("(2,'000001',N'国内名称','F1',N'Foreign','KDVoucherAUD'") && sql.Contains("(2,'000001',N'国内名称','000001',N'Foreign','KDVoucherAUD02'"),"generated SQL changes overseas code only for secondary ledger");
            Check(sql.Split(new[]{"INSERT INTO @Import"},StringSplitOptions.None).Length-1==(count*2+999)/1000,"doubled input respects 1000-row VALUES limit");
            Check(preview.Count==count && preview.All(r=>r.OverseasType==options.OverseasType) && preview.Select((r,i)=>r.FeeNoOverseas=="F"+(i+1) && r.FeeNameOverseas=="Foreign").All(x=>x),"preview remains primary-only and retains original overseas codes and names");
            int voucher=sql.LastIndexOf("INSERT INTO [dbo].[VoucherTypeRelation]",StringComparison.Ordinal),cash=sql.IndexOf("/********************现金流",StringComparison.Ordinal),subjects=sql.IndexOf("    -- 科目",StringComparison.Ordinal);
            Check(voucher<cash && cash<subjects,"cash and abstracts after voucher types before subjects");
        }
        var edited=new List<MappingRow>{Row(1,options.OverseasType)};
        edited[0].FeeNo="008801";edited[0].FeeNoOverseas="MANUAL";edited[0].FeeNameOverseas="Edited foreign name";
        var editedLedgers=MappingEngine.ForLedgers(edited,options.OverseasType);
        Check(editedLedgers[0].FeeNoOverseas=="MANUAL" && editedLedgers[1].FeeNoOverseas=="008801" && editedLedgers[1].FeeNameOverseas=="Edited foreign name" && edited[0].FeeNoOverseas=="MANUAL","secondary uses current edited domestic code while preserving main and preview foreign fields");
        string rendered=CashFlowTemplate.Render("DemoCNY","DemoCNY02","CNY");Parse(rendered);
        Check(rendered.Split(new[]{"INSERT INTO dbo.VoucherAuxiliaryRelation"},StringSplitOptions.None).Length-1==18,"18 auxiliary/cash statements preserved");
        Check(rendered.Split(new[]{"INSERT INTO dbo.VoucherAbstractRelation"},StringSplitOptions.None).Length-1==3,"three main ledger abstract statements preserved");
        Check(!rendered.Contains("KDVoucherAUD") && !rendered.Contains("'AUD'") && !rendered.Contains("{{"),"all requested placeholders replaced");
        Check(rendered.Split(new[]{"'DemoCNY02'"},StringSplitOptions.None).Length-1==10 && rendered.Split(new[]{"'DemoCNY'"},StringSplitOptions.None).Length-1==14,"auxiliary and abstract ledger assignments preserved with per-type existence checks");
        Check(rendered.Contains("'DemoCNY','CNY'") && rendered.Contains("getdate(),0"),"abstract currency follows UI and date/delete expressions retained");
        Check(rendered.Contains("[{\"key\":\"1111\",\"value\":\"CI01.01.01\"}") && rendered.Contains("[''客户'':[''number'':''{0}'']]"),"original cash codes and JSON/template payloads remain literal and unchanged");
        string escaped=CashFlowTemplate.Render("Demo'Type","Demo'Type02","U'SD");Parse(escaped);
        Check(escaped.Contains("'Demo''Type02'") && escaped.Contains("'U''SD'"),"cash mapping names and currency SQL-escaped");
        string markerType="Demo{{CURRENCY}}";
        Check(CashFlowTemplate.Render(markerType,markerType+"02","CNY").Contains("'Demo{{CURRENCY}}'"),"placeholder-like user text is not recursively substituted");
    }
}
