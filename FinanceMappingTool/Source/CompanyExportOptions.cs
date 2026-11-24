using System;
using System.Collections.Generic;
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
        // Subject/voucher type still follows base type + company code.
        public string OverseasType => ComposeMappingType(BaseMappingType, CompanyCode);
        public IReadOnlyList<string> AccountTypes => new[] { CompanyCode + "@01", CompanyCode + "@02" };
        public string Memo => "[" + CompanyCode + "]" + CompanyName;
        public CompanyExportOptions(string mappingType, string code, string name, string currency)
        { BaseMappingType = (mappingType ?? "").Trim(); CompanyCode = (code ?? "").Trim(); CompanyName = (name ?? "").Trim(); Currency = (currency ?? "").Trim(); }
        public static string ComposeMappingType(string mappingType, string companyCode)
        {
            string type = (mappingType ?? "").Trim(), code = (companyCode ?? "").Trim();
            Check(type, 50, true, true, "映射类型");
            // AccountType is varchar(20), including the fixed three-character @01 or @02 suffix.
            Check(code, 17, false, true, "公司编码");
            Check(type + code, 50, true, true, "最终映射类型");
            return type + code;
        }
        public string GetCompanyOverseasType(string accountType)
        {
            Check(BaseMappingType, 50, true, true, "映射类型");
            Check(CompanyCode, 17, true, true, "公司编码");
            Check(Currency, 50, true, true, "币制");
            string suffix;
            if (accountType == CompanyCode + "@01") suffix = "";
            else if (accountType == CompanyCode + "@02") suffix = "02";
            else throw new InvalidDataException("公司账套必须是当前公司编码加@01或@02。");
            string value = BaseMappingType + Currency + suffix;
            Check(value, 50, true, true, "凭证公司映射类型（" + accountType + "）");
            return value;
        }
        public void Validate()
        {
            ComposeMappingType(BaseMappingType, CompanyCode);
            if (!HasCompany) return;
            Check(Currency, 50, true, true, "币制");
            Check(Memo, 400, true, false, "公司备注（[公司编码]公司名称）");
            foreach (string account in AccountTypes) GetCompanyOverseasType(account);
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
