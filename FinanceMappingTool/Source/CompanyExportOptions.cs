using System;
using System.IO;
using System.Linq;
namespace FinanceMappingTool
{
    public sealed class CompanyExportOptions
    {
        public string BaseMappingType { get; }
        public string CompanyCode { get; }
        public string CompanyName { get; }
        public string Currency { get; }
        public bool HasCompany => CompanyCode.Length > 0;
        public string OverseasType => ComposeMappingType(BaseMappingType, CompanyCode);
        public string AccountType => CompanyCode + "@01";
        public string Memo => "[" + CompanyCode + "]" + CompanyName;
        public CompanyExportOptions(string mappingType, string code, string name, string currency)
        { BaseMappingType = (mappingType ?? "").Trim(); CompanyCode = (code ?? "").Trim(); CompanyName = (name ?? "").Trim(); Currency = (currency ?? "").Trim(); }
        public static string ComposeMappingType(string mappingType, string companyCode)
        {
            string type = (mappingType ?? "").Trim(), code = (companyCode ?? "").Trim();
            Check(type, 50, true, true, "映射类型");
            // AccountType is varchar(20), including its fixed three-character @01 suffix.
            Check(code, 17, false, true, "公司编码");
            Check(type + code, 50, true, true, "最终映射类型");
            return type + code;
        }
        public void Validate()
        {
            ComposeMappingType(BaseMappingType, CompanyCode);
            if (!HasCompany) return;
            Check(Currency, 50, true, true, "币制");
            Check(Memo, 400, true, false, "公司备注（[公司编码]公司名称）");
        }
        private static void Check(string value, int max, bool required, bool ascii, string label)
        {
            if (required && value.Length == 0) throw new InvalidDataException(label + "不能为空。");
            if (value.Length > max) throw new InvalidDataException(label + "超过字段长度限制" + max + "。");
            if (value.Any(c => c == '\0' || (ascii && (c < 32 || c > 126))))
                throw new InvalidDataException(label + "包含不支持的字符，请使用可打印ASCII字符填写编码及币制。");
        }
    }
}
