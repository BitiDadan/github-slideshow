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
        public string OverseasType => ComposeMappingType(BaseMappingType, Currency);
        public string SecondaryOverseasType => ComposeSecondaryMappingType(OverseasType);
        public IReadOnlyList<string> AccountTypes => new[] { CompanyCode + "@01", CompanyCode + "@02" };
        public string Memo => "[" + CompanyCode + "]" + CompanyName;
        public CompanyExportOptions(string mappingType, string code, string name, string currency)
        { BaseMappingType = (mappingType ?? "").Trim(); CompanyCode = (code ?? "").Trim(); CompanyName = (name ?? "").Trim(); Currency = (currency ?? "").Trim(); }
        public static string ComposeMappingType(string mappingType, string currency)
        {
            string type = (mappingType ?? "").Trim(), unit = (currency ?? "").Trim();
            Check(type, 50, true, true, "映射类型"); Check(unit, 50, true, true, "币制");
            Check(type + unit, 50, true, true, "最终映射类型"); return type + unit;
        }
        public static string ComposeSecondaryMappingType(string primary)
        {
            Check(primary, 50, true, true, "最终映射类型");
            Check(primary + "02", 50, true, true, "副账簿最终映射类型"); return primary + "02";
        }
        public string GetCompanyOverseasType(string accountType)
        {
            Check(CompanyCode, 17, true, true, "公司编码");
            if (accountType == CompanyCode + "@01") return OverseasType;
            if (accountType == CompanyCode + "@02") return SecondaryOverseasType;
            throw new InvalidDataException("公司账套必须是当前公司编码加@01或@02。");
        }
        public void Validate()
        {
            ComposeSecondaryMappingType(OverseasType);
            Check(CompanyCode, 17, false, true, "公司编码");
            if (HasCompany) Check(Memo, 400, true, false, "公司备注（[公司编码]公司名称）");
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
