using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinanceMappingTool;
using Microsoft.SqlServer.TransactSql.ScriptDom;
static class CompanyExportChecks
{
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static void Invalid(Action action,string name){bool bad=false;try{action();}catch(InvalidDataException){bad=true;}Check(bad,name);}
    static void Parse(string sql){IList<ParseError> errors;new TSql120Parser(true).Parse(new StringReader(sql),out errors);Check(errors.Count==0,"company SQL parses with SQL2014: "+string.Join(";",errors.Select(e=>e.Message)));}
    public static void Run()
    {
        string rule=Path.GetTempFileName();File.WriteAllText(rule,"<MappingRules />");
        try
        {
            var seed=new List<MappingRow>{new MappingRow{ExcelRow=2,FeeNo="0001",FeeName="国内名称",FeeNoOverseas="F001",FeeNameOverseas="Foreign",OverseasType="KDVoucher",Memo="",CustomerType="1",CreditDebitType=""}};
            var company=new CompanyExportOptions(" KDVoucher "," 000123 ","演示公司 O'Brien"," VND ");company.Validate();
            Check(company.OverseasType=="KDVoucher000123" && company.AccountType=="000123@01" && company.Memo=="[000123]演示公司 O'Brien","company code leading zero, suffix and memo composition");
            var rows=MappingEngine.ForType(seed,"KDVoucher",rule,company.CompanyCode);
            Check(rows[0].OverseasType==company.OverseasType && seed[0].OverseasType=="KDVoucher","subject and company effective mapping type match without mutating source");
            string sql=MappingEngine.Sql(rows,"DemoDB",company);Parse(sql);
            Check(sql.Contains("VALUES ('000123','000123@01','KDVoucher000123','VND',N'[000123]演示公司 O''Brien',0);"),"company INSERT field order, Unicode memo and quote escaping");
            Check(sql.Contains("[CompanyCode]='000123' AND [AccountType]='000123@01' AND [IsDeleted]=0") && sql.Contains("THROW 50006") && sql.Contains("THROW 50007"),"repeat company deployment checks active key, duplicates and conflicting contents");
            int transaction=sql.IndexOf("BEGIN TRANSACTION",StringComparison.Ordinal), companyInsert=sql.IndexOf("INSERT INTO [dbo].[VoucherCompanyRelation]",StringComparison.Ordinal);
            Check(transaction<companyInsert && companyInsert<sql.IndexOf("COMMIT TRANSACTION",StringComparison.Ordinal) && sql.Contains("ROLLBACK TRANSACTION"),"company and subject imports share transaction rollback");
            Check(sql.Contains("@companyInserted AS [公司关系新增条数]"),"company insert count included");
            var two=MappingEngine.ForType(seed,"KDVoucher02",rule,"000123");
            Check(two[0].FeeNoOverseas=="0001" && two[0].OverseasType=="KDVoucher02000123" && two[0].Memo=="02账套","02 conversion follows base type before company suffix is appended");
            Parse(MappingEngine.Sql(two,"DemoDB",new CompanyExportOptions("KDVoucher02","000123","Demo","CNY")));
            var noCompany=new CompanyExportOptions("KDVoucher","","","VND");
            string legacy=MappingEngine.Sql(seed,"DemoDB",noCompany);Parse(legacy);
            Check(noCompany.OverseasType=="KDVoucher" && !legacy.Contains("VoucherCompanyRelation"),"empty company code skips company relation and retains subject export");
            Invalid(()=>MappingEngine.Sql(seed,"DemoDB",company),"mismatched subject/company types block export");
            Invalid(()=>new CompanyExportOptions("Demo","123456789012345678","Demo","VND").Validate(),"AccountType length includes @01 suffix");
            Invalid(()=>CompanyExportOptions.ComposeMappingType(new string('A',45),"123456"),"combined mapping type length enforced");
            Invalid(()=>new CompanyExportOptions("Demo","001",new string('名',400),"VND").Validate(),"company Memo field length includes prefix");
            Invalid(()=>new CompanyExportOptions("Demo","001","Demo","").Validate(),"company currency required");
            Invalid(()=>CompanyExportOptions.ComposeMappingType("Demo","中文"),"varchar company code rejects unsupported characters");
            var quote=new CompanyExportOptions("Demo","A'1","中文'名称","VND");
            string escaped=MappingEngine.Sql(MappingEngine.ForType(seed,"Demo",rule,"A'1"),"DemoDB",quote);Parse(escaped);
            Check(escaped.Contains("'A''1@01'") && escaped.Contains("N'[A''1]中文''名称'"),"all company inputs SQL-escaped");
        }
        finally{File.Delete(rule);}
    }
}
