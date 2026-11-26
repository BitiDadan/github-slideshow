using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using ClosedXML.Excel;
using FinanceMappingTool;
using Microsoft.SqlServer.TransactSql.ScriptDom;

static class CustomerTypeChecks
{
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static void Invalid(Action action,string name){bool failed=false;try{action();}catch(InvalidDataException){failed=true;}Check(failed,name);}
    static void Parse(string sql){IList<ParseError> errors;new TSql120Parser(true).Parse(new StringReader(sql),out errors);Check(errors.Count==0,"customer type SQL2014 syntax");}
    static bool HasRow(string sql,MappingRow row,string type,string foreign,string customer)
    {return sql.Contains("'"+row.FeeNo+"',N'Domestic','"+foreign+"',N'Foreign','"+type+"',N'',"+customer+",");}
    public static void Run()
    {
        string dir=Path.Combine(Path.GetTempPath(),"finance-customers-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        string rules=Path.Combine(dir,"mapping-rules.xml"),file=Path.Combine(dir,"input.xlsx");
        File.WriteAllText(rules,"<MappingRules />");
        var required=new Dictionary<string,string>{{"2203","1"},{"220301","1"},{"220399","1"},{"1123","2"},{"112301","2"},{"112302","2"},{"11230399","2"},{"112398","2"},{"6401","2"},{"640101","2"}};
        var company=new CompanyExportOptions("Demo","090102","Demo","AUD");
        try
        {
            Check(CustomerTypeRules.Load(rules).Count==10,"older config without section receives ten default customer rules");
            foreach(string kind in new[]{"资产","损益","负债","成本",""," 损益 ","损益类","权益"})
            {
                string baseline=new[]{"资产","损益","权益"}.Contains(kind.Trim())?"1":"2";
                using(var book=new XLWorkbook())
                {
                    var s=book.AddWorksheet("Demo");
                    foreach(var field in ExcelSchema.Fields.Select((f,i)=>new{f,i}))s.Cell(1,field.i+1).Value=field.f.Header;
                    var codes=required.Keys.Concat(new[]{"9000","06401","64010101","22030199"}).ToList();
                    for(int i=0;i<codes.Count;i++)
                    {s.Cell(i+2,1).Value="F"+i;s.Cell(i+2,2).Value="Foreign";s.Cell(i+2,3).Value=codes[i];s.Cell(i+2,4).Value="Domestic";s.Cell(i+2,5).Value=kind;}
                    book.SaveAs(file);
                }
                var rows=MappingEngine.Read(file,"Demo",1,rules).Rows;
                Check(rows.Where(r=>required.ContainsKey(r.FeeNo)).All(r=>r.CustomerType==required[r.FeeNo]),"ten exact-code exceptions beat subject type "+kind);
                Check(rows.Where(r=>!required.ContainsKey(r.FeeNo)).All(r=>r.CustomerType==baseline && r.DefaultCustomerType==baseline),"default kind rule and non-prefix matching for "+kind);
                var preview=MappingEngine.ForType(rows,"Demo",rules,"AUD");
                string sql=MappingEngine.Sql(preview,"DemoDB",company,CustomerTypeRules.Load(rules));Parse(sql);
                Check(preview.All(r=>HasRow(sql,r,"DemoAUD",r.FeeNoOverseas,r.CustomerType) && HasRow(sql,r,"DemoAUD02",r.FeeNo,r.CustomerType)),"both SQL ledgers retain preview customer values for "+kind);
            }
            var original=MappingEngine.Read(file,"Demo",1,rules).Rows;
            var legacy=new XElement("Override",new XAttribute("enabled","true"),new XAttribute("sourceFeeNo","2203"),new XAttribute("sourceOverseas",original.Single(r=>r.FeeNo=="2203").FeeNoOverseas),new XAttribute("customerType","2"));
            new XDocument(new XElement("MappingRules",legacy,new XElement("Type02Override",new XAttribute("feeNo","9000"),new XAttribute("overseas","OTHER")))).Save(rules);
            Check(MappingEngine.Read(file,"Demo",1,rules).Rows.Single(r=>r.FeeNo=="2203").CustomerType=="1","customer exceptions override matching import repair customer value");
            var custom=new Dictionary<string,string>{{"2203","2"},{"6401","1"},{"06401","2"}};
            CustomerTypeRules.Save(rules,custom);
            var reloaded=CustomerTypeRules.Load(rules);
            Check(reloaded.Count==3 && reloaded["06401"]=="2" && reloaded["6401"]=="1","saved custom rules reload with leading zeros and edited defaults");
            Check(XDocument.Load(rules).Root.Elements("Override").Count()==1 && XDocument.Load(rules).Root.Elements("Type02Override").Count()==1,"saving customer rules preserves other mapping configuration");
            var changed=MappingEngine.ForType(MappingEngine.Read(file,"Demo",1,rules).Rows,"Demo",rules,"AUD");
            Check(changed.Single(r=>r.FeeNo=="2203").CustomerType=="2" && changed.Single(r=>r.FeeNo=="6401").CustomerType=="1" && changed.Single(r=>r.FeeNo=="640101").CustomerType=="1","edited rules apply and removed default uses its original subject kind");
            var removed=changed.Single(r=>r.FeeNo=="640101");removed.CustomerType="2";
            var unrelated=changed.Single(r=>r.FeeNo=="9000");unrelated.CustomerType="1";unrelated.FeeNameOverseas="Edited name";
            MappingEngine.RefreshCustomerTypeExceptions(changed,CustomerTypeRules.Defaults,reloaded);
            Check(removed.CustomerType=="1" && unrelated.CustomerType=="1" && unrelated.FeeNameOverseas=="Edited name","removing rule restores baseline and keeps unrelated manual edits");
            var forced=changed.Single(r=>r.FeeNo=="6401");forced.CustomerType="2";
            string customSql=MappingEngine.Sql(changed,"DemoDB",company,reloaded);Parse(customSql);
            Check(HasRow(customSql,forced,"DemoAUD",forced.FeeNoOverseas,"1") && HasRow(customSql,forced,"DemoAUD02",forced.FeeNo,"1") && forced.CustomerType=="2","custom rules apply to SQL copies without mutating caller preview");
            CustomerTypeRules.Save(rules,new Dictionary<string,string>());
            Check(CustomerTypeRules.Load(rules).Count==0,"deleting all exceptions persists an authoritative empty section");
            Check(MappingEngine.Read(file,"Demo",1,rules).Rows.Single(r=>r.FeeNo=="2203").CustomerType=="2","empty section does not restore built-in code exceptions");
            byte[] before=File.ReadAllBytes(rules);
            Invalid(()=>CustomerTypeRules.Save(rules,new Dictionary<string,string>{{"6401","3"}}),"invalid customer value cannot be saved");
            Check(before.SequenceEqual(File.ReadAllBytes(rules)),"failed save leaves prior config intact");
            Invalid(()=>CustomerTypeRules.Validate(new[]{new KeyValuePair<string,string>("001","1"),new KeyValuePair<string,string>("001","2")}),"duplicate code rejected");
            Invalid(()=>CustomerTypeRules.Validate(new Dictionary<string,string>{{new string('1',21),"1"}}),"overlong code rejected");
        }
        finally{Directory.Delete(dir,true);}
    }
}
