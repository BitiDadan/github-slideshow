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
            Check(company.OverseasType=="KDVoucherVND" && company.AccountTypes.SequenceEqual(new[]{"123@01","123@02"}) && company.Memo=="[000123]演示公司 O'Brien","company code leading zero, suffix and memo composition");
            var rows=MappingEngine.ForType(seed,"KDVoucher",rule,company.Currency);
            Check(rows[0].OverseasType==company.OverseasType && seed[0].OverseasType=="KDVoucher","subject effective mapping type follows base plus currency without mutating source");
            string sql=MappingEngine.Sql(rows,"DemoDB",company);Parse(sql);
            Check(company.GetCompanyOverseasType("123@01")=="KDVoucherVND" && company.GetCompanyOverseasType("123@02")=="KDVoucherVND02","company OverseasType uses currency and adds 02 only for second account");
            foreach(string account in new[]{"123@01","123@02"})
                Check(sql.Contains("VALUES ('000123','"+account+"','"+company.GetCompanyOverseasType(account)+"','"+(account.EndsWith("@02")?"CNY":"VND")+"',N'[000123]演示公司 O''Brien',0);"),"company tuple uses per-ledger mapping type and currency: "+account);
            Check(sql.Contains("CONVERT(varbinary(max),CASE [AccountType] WHEN '123@01' THEN 'VND' WHEN '123@02' THEN 'CNY' END)"),"existing company currency conflict checks expect UI currency for main and CNY for secondary");
            Check(sql.Contains("WHEN '123@01' THEN 'KDVoucherVND'") && sql.Contains("WHEN '123@02' THEN 'KDVoucherVND02'"),"existing company checks use per-account expected OverseasType");
            Check(rows[0].OverseasType=="KDVoucherVND" && sql.Contains("VALUES ('A','A','KDVoucherVND','');") && sql.Contains("VALUES ('A','A','KDVoucherVND02','');"),"subject and voucher types use currency-based main and secondary ledgers");
            Check(sql.Split(new[]{"INSERT INTO [dbo].[VoucherCompanyRelation]"},StringSplitOptions.None).Length==3,"exactly two company INSERT statements");
            Check(sql.Contains("GROUP BY [AccountType] HAVING COUNT_BIG(*)>1") &&
                sql.Contains("IF NOT EXISTS (SELECT 1 FROM @CompanyExisting WHERE [AccountType]='123@01')") &&
                sql.Contains("IF NOT EXISTS (SELECT 1 FROM @CompanyExisting WHERE [AccountType]='123@02')"),"duplicate/existence checks distinguish the two accounts");
            Check(sql.Split(new[]{"SET @companyInserted += 1;"},StringSplitOptions.None).Length==3 && !sql.Contains("SET @companyInserted = 1;"),"company insert count accumulates both new accounts");
            Check(sql.Contains("[CompanyCode]='000123' AND [AccountType] IN ('123@01','123@02') AND [IsDeleted]=0") && sql.Contains("THROW 50006") && sql.Contains("THROW 50007"),"repeat company deployment checks active key, duplicates and conflicting contents");
            int transaction=sql.IndexOf("BEGIN TRANSACTION",StringComparison.Ordinal), companyInsert=sql.IndexOf("INSERT INTO [dbo].[VoucherCompanyRelation]",StringComparison.Ordinal);
            Check(transaction<companyInsert && companyInsert<sql.IndexOf("COMMIT TRANSACTION",StringComparison.Ordinal) && sql.Contains("ROLLBACK TRANSACTION"),"company and subject imports share transaction rollback");
            Check(sql.Contains("@companyInserted AS [公司关系新增条数]"),"company insert count included");
            var two=MappingEngine.ForType(seed,"KDVoucher02",rule,"CNY");
            Check(two[0].FeeNoOverseas=="0001" && two[0].OverseasType=="KDVoucher02CNY" && two[0].Memo=="02账套","explicit legacy 02 conversion follows base type before currency is appended");
            Parse(MappingEngine.Sql(two,"DemoDB",new CompanyExportOptions("KDVoucher02","000123","Demo","CNY")));
            var noCompany=new CompanyExportOptions("KDVoucher","","","VND");
            string legacy=MappingEngine.Sql(MappingEngine.ForType(seed,"KDVoucher",rule,"VND"),"DemoDB",noCompany);Parse(legacy);
            Check(noCompany.OverseasType=="KDVoucherVND" && !legacy.Contains("VoucherCompanyRelation"),"empty company code skips company relation and retains subject export");
            Invalid(()=>MappingEngine.Sql(seed,"DemoDB",company),"subject type inconsistent with its base and currency blocks export");
            Invalid(()=>new CompanyExportOptions("Demo","123456789012345678","Demo","VND").Validate(),"AccountType length includes @01/@02 suffix");
            Invalid(()=>CompanyExportOptions.ComposeMappingType(new string('A',45),"123456"),"combined mapping type length enforced");
            Invalid(()=>new CompanyExportOptions("Demo","001",new string('名',400),"VND").Validate(),"company Memo field length includes prefix");
            Invalid(()=>new CompanyExportOptions("Demo","001","Demo","").Validate(),"company currency required");
            Invalid(()=>CompanyExportOptions.ComposeMappingType("Demo","中文"),"currency rejects unsupported characters");
            var aud=new CompanyExportOptions("KDVoucher","000123","Demo","AUD");aud.Validate();
            Check(aud.OverseasType=="KDVoucherAUD" && aud.SecondaryOverseasType=="KDVoucherAUD02", "UI mapping types use currency and fixed secondary suffix");
            Check(new CompanyExportOptions("KDVoucher","999","Demo","AUD").OverseasType==aud.OverseasType,"changing company code does not change mapping type");
            Invalid(()=>new CompanyExportOptions("Demo","","","").Validate(),"currency required even with no company code");
            Check(aud.GetCompanyOverseasType("123@01")=="KDVoucherAUD" && aud.GetCompanyOverseasType("123@02")=="KDVoucherAUD02","AUD company mappings match requested rule");
            foreach(string unit in new[]{"AUD","VND","CNY"})
            {
                var perCurrency=new CompanyExportOptions("KDVoucher","000123","Demo",unit);
                string ledgerSql=MappingEngine.Sql(MappingEngine.ForType(seed,"KDVoucher",rule,unit),"DemoDB",perCurrency);Parse(ledgerSql);
                Check(perCurrency.GetCompanyCurrency("123@01")==unit && perCurrency.GetCompanyCurrency("123@02")=="CNY","secondary currency stays CNY for UI currency "+unit);
                Check(ledgerSql.Contains("VALUES ('000123','123@01','KDVoucher"+unit+"','"+unit+"',N'[000123]Demo',0);") &&
                    ledgerSql.Contains("VALUES ('000123','123@02','KDVoucher"+unit+"02','CNY',N'[000123]Demo',0);"),"company inserts preserve mapping naming and set ledger currency for "+unit);
            }
            Invalid(()=>aud.GetCompanyOverseasType("123@03"),"unexpected company account rejected");
            Invalid(()=>aud.GetCompanyCurrency("123@03"),"unexpected company currency account rejected");
            new CompanyExportOptions(new string('A',45),"1","Demo","VND").Validate();
            Invalid(()=>new CompanyExportOptions(new string('A',46),"1","Demo","VND").Validate(),"company type length includes currency and second-account 02 suffix");
            var quote=new CompanyExportOptions("Demo","A'1","中文'名称","VND");
            string escaped=MappingEngine.Sql(MappingEngine.ForType(seed,"Demo",rule,"VND"),"DemoDB",quote);Parse(escaped);
            Check(escaped.Contains("'A''1@01'") && escaped.Contains("'A''1@02'") && escaped.Contains("N'[A''1]中文''名称'"),"all company inputs SQL-escaped");
            foreach(string code in new[]{"090102","90102","000000","0"})
            {
                string expected=(code=="000000" || code=="0")?"0":"90102";
                var normalized=new CompanyExportOptions("KDVoucher",code,"Demo","AUD");normalized.Validate();
                Check(normalized.AccountTypes.SequenceEqual(new[]{expected+"@01",expected+"@02"}) && normalized.CompanyCode==code && normalized.Memo=="["+code+"]Demo","normalize only account prefix without changing original company fields: "+code);
                string normalizedSql=MappingEngine.Sql(MappingEngine.ForType(seed,"KDVoucher",rule,"AUD"),"DemoDB",normalized);Parse(normalizedSql);
                Check(normalizedSql.Contains("VALUES ('"+code+"','"+expected+"@01','KDVoucherAUD','AUD',N'["+code+"]Demo',0);") &&
                    normalizedSql.Contains("VALUES ('"+code+"','"+expected+"@02','KDVoucherAUD02','CNY',N'["+code+"]Demo',0);"),"normalized main and secondary INSERT values: "+code);
                Check(normalizedSql.Contains("[CompanyCode]='"+code+"' AND [AccountType] IN ('"+expected+"@01','"+expected+"@02')") &&
                    normalizedSql.Contains("WHEN '"+expected+"@02' THEN 'CNY'"),"normalized account lookup and expected currency: "+code);
            }
            Invalid(()=>new CompanyExportOptions("Demo","090102","Demo","AUD").GetCompanyCurrency("090102@02"),"unnormalized account rejected by currency lookup");
        }
        finally{File.Delete(rule);}
    }
}
