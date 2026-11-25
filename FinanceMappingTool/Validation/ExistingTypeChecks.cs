using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FinanceMappingTool;
using Microsoft.SqlServer.TransactSql.ScriptDom;

// Inspect and evaluate the generated IF branches; this does not execute SQL Server.
static class ExistingTypeChecks
{
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    sealed class Guard
    {
        public string Table, Type;
        public TSqlStatement Present, Missing;
    }
    sealed class Guards : TSqlFragmentVisitor
    {
        public readonly List<Guard> Items=new List<Guard>();
        public override void ExplicitVisit(IfStatement node)
        {
            var exists=node.Predicate as ExistsPredicate;
            var query=exists?.Subquery.QueryExpression as QuerySpecification;
            var table=query?.FromClause?.TableReferences.SingleOrDefault() as NamedTableReference;
            string name=table?.SchemaObject?.BaseIdentifier.Value;
            if(new[]{"VoucherTypeRelation","VoucherAuxiliaryRelation","VoucherAbstractRelation"}.Contains(name))
            {
                var comparison=query.WhereClause.SearchCondition as BooleanComparisonExpression;
                var column=comparison?.FirstExpression as ColumnReferenceExpression;
                var value=comparison?.SecondExpression as StringLiteral;
                Check(comparison!=null && comparison.ComparisonType==BooleanComparisonType.Equals && column?.MultiPartIdentifier.Identifiers.Last().Value=="OverseasType" && value!=null,"existence guard uses OverseasType alone for "+name);
                Items.Add(new Guard{Table=name,Type=value.Value,Present=node.ThenStatement,Missing=node.ElseStatement});
            }
            base.ExplicitVisit(node);
        }
    }
    sealed class BranchResult
    {
        public readonly Dictionary<string,int> Inserts=new Dictionary<string,int>();
        public readonly Dictionary<string,int> Counters=new Dictionary<string,int>();
        int lastRows;
        public void Apply(TSqlStatement statement)
        {
            var block=statement as BeginEndBlockStatement;
            if(block!=null){foreach(var child in block.StatementList.Statements)Apply(child);return;}
            var insert=statement as InsertStatement;
            if(insert!=null)
            {
                string table=((NamedTableReference)insert.InsertSpecification.Target).SchemaObject.BaseIdentifier.Value;
                lastRows=((ValuesInsertSource)insert.InsertSpecification.InsertSource).RowValues.Count;
                Inserts[table]=Get(Inserts,table)+lastRows;return;
            }
            var set=statement as SetVariableStatement;
            if(set!=null)
            {
                if(set.AssignmentKind.ToString()!="AddEquals")throw new Exception("Branch counter must use additive accounting");
                var literal=set.Expression as IntegerLiteral;
                var global=set.Expression as GlobalVariableExpression;
                int amount=literal!=null?int.Parse(literal.Value):global?.Name=="@@ROWCOUNT"?lastRows:throw new Exception("Unexpected branch counter expression");
                Counters[set.Variable.Name]=Get(Counters,set.Variable.Name)+amount;return;
            }
            throw new Exception("Unexpected branch control flow: "+statement?.GetType().Name);
        }
    }
    static int Get(Dictionary<string,int> values,string key){return values.TryGetValue(key,out int n)?n:0;}
    public static void Run()
    {
        var company=new CompanyExportOptions("Demo","090102","Demo","AUD");
        var row=new MappingRow{ExcelRow=2,FeeNo="0001",FeeName="Local",FeeNoOverseas="F1",FeeNameOverseas="Foreign",OverseasType=company.OverseasType,CustomerType="1",CreditDebitType="",Memo=""};
        string sql=MappingEngine.Sql(new List<MappingRow>{row},"DemoDB",company);
        IList<ParseError> errors;var ast=new TSql120Parser(true).Parse(new StringReader(sql),out errors);
        Check(errors.Count==0,"existing type checks parse as SQL Server 2014");
        var guards=new Guards();ast.Accept(guards);
        Check(guards.Items.Count==5 && guards.Items.Select(g=>g.Table+"/"+g.Type).Distinct().Count()==5,"five independent guards: two voucher, two cash and one primary abstract");
        var scenarios=new[]{new bool[]{false,false,false,false,false},new bool[]{true,false,false,false,false},new bool[]{true,false,true,false,true},new bool[]{false,true,true,false,false},new bool[]{true,true,true,true,true}};
        foreach(var scenario in scenarios)
        {
            var result=new BranchResult();
            for(int i=0;i<guards.Items.Count;i++)result.Apply(scenario[i]?guards.Items[i].Present:guards.Items[i].Missing);
            int voucherSkip=(scenario[0]?9:0)+(scenario[1]?9:0),cashSkip=(scenario[2]?9:0)+(scenario[3]?9:0),abstractSkip=scenario[4]?3:0;
            Check(Get(result.Inserts,"VoucherTypeRelation")==18-voucherSkip && Get(result.Inserts,"VoucherAuxiliaryRelation")==18-cashSkip && Get(result.Inserts,"VoucherAbstractRelation")==3-abstractSkip,"existing groups skip without affecting other groups: "+string.Join(",",scenario));
            Check(Get(result.Counters,"@voucherTypesInserted")==18-voucherSkip && Get(result.Counters,"@voucherTypesSkipped")==voucherSkip && Get(result.Counters,"@cashFlowInserted")==18-cashSkip && Get(result.Counters,"@cashFlowSkipped")==cashSkip && Get(result.Counters,"@abstractInserted")==3-abstractSkip && Get(result.Counters,"@abstractSkipped")==abstractSkip,"all inserted and skipped counters match selected branches");
        }
        foreach(string label in new[]{"凭证类型新增条数","凭证类型跳过条数","现金流新增条数","现金流跳过条数","凭证摘要新增条数","凭证摘要跳过条数"})
            Check(sql.Contains(" AS ["+label+"]"),"final execution summary includes "+label);
        Check(sql.LastIndexOf("    -- 科目",StringComparison.Ordinal)>sql.IndexOf("--副账簿 默认CNY",StringComparison.Ordinal) && sql.Contains("COMMIT TRANSACTION"),"subject processing and commit follow guarded groups");
    }
}
