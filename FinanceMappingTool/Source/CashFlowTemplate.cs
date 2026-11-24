using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace FinanceMappingTool
{
    public static class CashFlowTemplate
    {
        private static string Literal(string value) { return "'" + value.Replace("'", "''") + "'"; }
        public static string Render(string primaryType, string secondaryType, string currency)
        {
            if (CompanyExportOptions.ComposeSecondaryMappingType(primaryType) != secondaryType)
                throw new InvalidDataException("现金流副账簿类型必须为主账簿类型追加02。");
            if (string.IsNullOrWhiteSpace(currency)) throw new InvalidDataException("现金流及凭证摘要的币制不能为空。");
            using (var stream = typeof(CashFlowTemplate).Assembly.GetManifestResourceStream("FinanceMappingTool.cash-flow-template.sql"))
            {
                if (stream == null) throw new InvalidDataException("未找到内置现金流SQL模板，请重新编译完整工程。");
                using (var reader = new StreamReader(stream))
                {
                    var values = new Dictionary<string,string> { {"{{PRIMARY_TYPE}}",Literal(primaryType)},
                        {"{{SECONDARY_TYPE}}",Literal(secondaryType)},{"{{CURRENCY}}",Literal(currency)} };
                    return Regex.Replace(reader.ReadToEnd(), @"\{\{(?:PRIMARY_TYPE|SECONDARY_TYPE|CURRENCY)\}\}", m => values[m.Value]);
                }
            }
        }
    }
}
