using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FinanceMappingTool
{
    public static class CustomerTypeRules
    {
        public static IReadOnlyDictionary<string, string> Defaults { get; } = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal) {
                {"2203","1"},{"220301","1"},{"220399","1"},
                {"1123","2"},{"112301","2"},{"112302","2"},{"11230399","2"},{"112398","2"},
                {"6401","2"},{"640101","2"}
            });
        public static Dictionary<string, string> Validate(IEnumerable<KeyValuePair<string, string>> rules)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                string code = (rule.Key ?? "").Trim(), value = (rule.Value ?? "").Trim();
                if (code.Length == 0 || code.Length > 20 || code.Any(c => c < 33 || c > 126))
                    throw new InvalidDataException("国内科目编码须为1～20个可打印ASCII字符，不能包含空格；编码按文本保存。");
                if (value != "1" && value != "2") throw new InvalidDataException("科目 " + code + " 的客户/供应商只能设置为1（客户）或2（供应商）。");
                if (result.ContainsKey(code)) throw new InvalidDataException("国内科目编码重复：" + code);
                result.Add(code, value);
            }
            return result;
        }
        public static Dictionary<string, string> FromRoot(XElement root)
        {
            if (root == null || root.Name != "MappingRules") throw new InvalidDataException("规则文件根节点必须为MappingRules。");
            var sections = root.Elements("CustomerTypeExceptions").ToList();
            if (sections.Count > 1) throw new InvalidDataException("规则文件存在重复的CustomerTypeExceptions节点。");
            if (sections.Count == 0) return Validate(Defaults);
            return Validate(sections[0].Elements("Rule").Select(e =>
                new KeyValuePair<string,string>((string)e.Attribute("feeNo"), (string)e.Attribute("customerType"))));
        }
        public static Dictionary<string, string> Load(string file)
        { return File.Exists(file) ? FromRoot(XDocument.Load(file).Root) : Validate(Defaults); }
        public static void Save(string file, IEnumerable<KeyValuePair<string, string>> rules)
        {
            var values = Validate(rules);
            var doc = File.Exists(file) ? XDocument.Load(file) : new XDocument(new XElement("MappingRules"));
            FromRoot(doc.Root); // Validate the existing file before replacing its section.
            var section = new XElement("CustomerTypeExceptions", values.Select(p =>
                new XElement("Rule", new XAttribute("feeNo", p.Key), new XAttribute("customerType", p.Value))));
            var old = doc.Root.Element("CustomerTypeExceptions");
            if (old == null) doc.Root.Add(section); else old.ReplaceWith(section);
            string target = Path.GetFullPath(file), temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                doc.Save(temp);
                if (File.Exists(target)) File.Replace(temp, target, null); else File.Move(temp, target);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
