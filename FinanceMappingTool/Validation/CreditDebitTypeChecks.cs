using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FinanceMappingTool;
using Microsoft.SqlServer.TransactSql.ScriptDom;
static class CreditDebitTypeChecks
{
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static MappingRow Row(string code,string credit){return new MappingRow{ExcelRow=2,FeeNo=code,FeeName="自定义科目名称",FeeNoOverseas="FOREIGN",FeeNameOverseas="Foreign name",OverseasType="DemoAUD",CustomerType="1",CreditDebitType=credit,Memo="keep"};}
    public static void Run()
    {
        var expected=new Dictionary<string,string>{
            {"112202","1"},{"1312","1"},{"122101","4"},{"12210403","1"},{"122108","1"},{"112301","2"},
            {"112302","2"},{"220202","2"},{"3312","2"},{"220301","1"},{"224105","2"},{"224106","2"}};
        var preview=expected.Keys.Select(code=>Row(code,"9")).ToList();
        preview.Add(Row("999999","3"));preview.Add(Row("999998",""));preview.Add(Row("0112202","3"));
        string sql=MappingEngine.Sql(preview,"DemoDB",new CompanyExportOptions("Demo","000123","Demo","AUD"));
        IList<ParseError> errors;new TSql120Parser(true).Parse(new StringReader(sql),out errors);
        Check(errors.Count==0,"fixed credit/debit SQL parses as SQL Server 2014");
        foreach(var pair in expected)
        {
            string main="'"+pair.Key+"',N'自定义科目名称','FOREIGN',N'Foreign name','DemoAUD',N'keep',1,"+pair.Value+")";
            string secondary="'"+pair.Key+"',N'自定义科目名称','"+pair.Key+"',N'Foreign name','DemoAUD02',N'keep',1,"+pair.Value+")";
            Check(sql.Contains(main) && sql.Contains(secondary),"SQL-only fixed credit/debit value on both ledgers for "+pair.Key+" = "+pair.Value);
        }
        foreach(var row in preview.Where(r=>!expected.ContainsKey(r.FeeNo)))
        {
            string credit=string.IsNullOrEmpty(row.CreditDebitType)?"NULL":row.CreditDebitType;
            Check(sql.Contains("'"+row.FeeNo+"',N'自定义科目名称','FOREIGN',N'Foreign name','DemoAUD',N'keep',1,"+credit+")") &&
                sql.Contains("'"+row.FeeNo+"',N'自定义科目名称','"+row.FeeNo+"',N'Foreign name','DemoAUD02',N'keep',1,"+credit+")"),"unlisted code retains its original credit/debit value on both ledgers: "+row.FeeNo);
        }
        Check(preview.Where(r=>expected.ContainsKey(r.FeeNo)).All(r=>r.CreditDebitType=="9") && preview.All(r=>r.FeeName=="自定义科目名称" && r.FeeNoOverseas=="FOREIGN" && r.OverseasType=="DemoAUD"),"SQL export does not mutate preview attributes or replace subject names");
    }
}
