using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinanceMappingTool;
using Microsoft.SqlServer.TransactSql.ScriptDom;
static class VoucherTypeChecks
{
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static void Parse(string sql){IList<ParseError> errors;new TSql120Parser(true).Parse(new StringReader(sql),out errors);Check(errors.Count==0,"voucher types SQL2014 syntax: "+string.Join(";",errors.Select(e=>e.Message)));}
    static MappingRow Row(string type){return new MappingRow{ExcelRow=2,FeeNo="0001",FeeName="Demo",FeeNoOverseas="001",FeeNameOverseas="Foreign",OverseasType=type,CustomerType="1",CreditDebitType="",Memo=""};}
    public static void Run()
    {
        string[] codes={"A","B","C","D","E","F","G","H","01"};
        var company=new CompanyExportOptions("DemoType","000123","演示公司","AUD");
        string sql=MappingEngine.Sql(new List<MappingRow>{Row(company.OverseasType)},"DemoDB",company);Parse(sql);
        int first=sql.IndexOf("-- 凭证公司",StringComparison.Ordinal),second=sql.IndexOf("-- 凭证类型",StringComparison.Ordinal),third=sql.IndexOf("-- 科目",StringComparison.Ordinal);
        Check(first>=0 && first<second && second<third,"exact comments order: company, voucher type, subject");
        int companyInsert=sql.IndexOf("INSERT INTO [dbo].[VoucherCompanyRelation]",StringComparison.Ordinal),typeInsert=sql.IndexOf("INSERT INTO [dbo].[VoucherTypeRelation]",StringComparison.Ordinal),feeInsert=sql.IndexOf("INSERT INTO [dbo].[VoucherFeeRelation]",StringComparison.Ordinal);
        Check(sql.IndexOf("RAISERROR(N'导入会话 SPID=",StringComparison.Ordinal)<sql.IndexOf("INSERT INTO @CompanyExisting",StringComparison.Ordinal),"session diagnostic printed before new database reads");
        Check(companyInsert<typeInsert && sql.LastIndexOf("INSERT INTO [dbo].[VoucherCompanyRelation]",StringComparison.Ordinal)<typeInsert && typeInsert<sql.IndexOf("INSERT INTO @Import",StringComparison.Ordinal) && typeInsert<feeInsert,"company and types appear before even subject staging data");
        Check(sql.IndexOf("BEGIN TRANSACTION",StringComparison.Ordinal)<companyInsert && feeInsert<sql.IndexOf("COMMIT TRANSACTION",StringComparison.Ordinal),"three business tables share transaction");
        Check(sql.Split(new[]{"INSERT INTO [dbo].[VoucherTypeRelation]"},StringSplitOptions.None).Length==10,"exactly nine explicit voucher type inserts");
        int previous=-1;
        foreach(string code in codes)
        {
            string tuple="VALUES ('"+code+"','"+code+"','DemoType000123','');";
            int at=sql.IndexOf(tuple,StringComparison.Ordinal);Check(at>previous,"voucher type pair and ordering: "+code);previous=at;
        }
        Check(sql.Contains("VALUES ('01','01','DemoType000123','');"),"01 remains text with leading zero");
        string typeBlock=sql.Substring(second,third-second);
        Check(!sql.Contains("@VoucherTypeExisting") && !sql.Contains("FROM [dbo].[VoucherTypeRelation]") && !sql.Contains("THROW 50008") && !sql.Contains("THROW 50009"),"voucher types do not query or validate existing database rows");
        Check(!typeBlock.Contains("IF ") && !typeBlock.Contains("SELECT ") && !typeBlock.Contains("EXISTS"),"all nine voucher type statements are unconditional direct inserts");
        Check(sql.Contains("@voucherTypesInserted AS [凭证类型新增条数]"),"voucher type inserted count included");
        string legacy=MappingEngine.Sql(new List<MappingRow>{Row("KDVoucher")},"DemoDB");Parse(legacy);
        Check(!legacy.Contains("INSERT INTO [dbo].[VoucherCompanyRelation]") && legacy.Contains("VALUES ('01','01','KDVoucher','');"),"blank company still generates nine voucher types for base mapping");
        string multi=MappingEngine.Sql(new List<MappingRow>{Row("Type1"),Row("Type2")},"DemoDB");Parse(multi);
        Check(multi.Split(new[]{"INSERT INTO [dbo].[VoucherTypeRelation]"},StringSplitOptions.None).Length==19,"distinct effective mapping types each get nine voucher pairs");
        string quote=MappingEngine.Sql(new List<MappingRow>{Row("Demo'Type")},"DemoDB");Parse(quote);
        Check(quote.Contains("VALUES ('A','A','Demo''Type','');"),"voucher type uses escaped effective mapping type");
    }
}
