using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FinanceMappingTool
{
    public sealed class CustomerTypeRuleForm : Form
    {
        private sealed class RuleRow
        {
            public string FeeNo { get; set; }
            public string CustomerType { get; set; }
            public string DisplayType => CustomerType == "1" ? "1（客户）" : "2（供应商）";
        }
        private readonly BindingList<RuleRow> items;
        private readonly TextBox code = new TextBox { Width = 180, MaxLength = 20 };
        private readonly ComboBox type = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true,
            AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White };
        public IReadOnlyDictionary<string, string> Rules { get; private set; }
        public CustomerTypeRuleForm(string ruleFile, IReadOnlyDictionary<string, string> initial)
        {
            Text = "设置客户/供应商特例"; Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterParent;
            Size = new Size(730, 560); MinimumSize = new Size(650, 440);
            items = new BindingList<RuleRow>(initial.Select(p => new RuleRow { FeeNo = p.Key, CustomerType = p.Value }).ToList());
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(670, 0), Text =
                "按完整国内科目编码指定客户/供应商，特例优先于科目类型。编码按文本保存，保留前导零。\r\n选中下方记录可修改；删除特例后恢复科目类型规则。保存后同步当前预览，并在下次启动继续使用。" }, 0, 0);
            var edit = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            edit.Controls.Add(new Label { Text = "国内科目编码", AutoSize = true, Margin = new Padding(0, 7, 3, 0) }); edit.Controls.Add(code);
            type.Items.AddRange(new object[] { "1（客户）", "2（供应商）" }); type.SelectedIndex = 0; edit.Controls.Add(type);
            var upsert = new Button { Text = "新增/更新", AutoSize = true }; edit.Controls.Add(upsert); layout.Controls.Add(edit, 0, 1);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; grid.ColumnHeadersHeight = 40;
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "国内科目编码", DataPropertyName = "FeeNo" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "客户/供应商（CustomerType）", DataPropertyName = "DisplayType" });
            grid.DataSource = items; layout.Controls.Add(grid, 0, 2);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
            var save = new Button { Text = "保存并应用", AutoSize = true };
            var remove = new Button { Text = "删除选中", AutoSize = true };
            actions.Controls.Add(cancel); actions.Controls.Add(save); actions.Controls.Add(remove); layout.Controls.Add(actions, 0, 3);
            Controls.Add(layout); CancelButton = cancel; AcceptButton = upsert;
            Action showSelected = () =>
            {
                var row = grid.CurrentRow?.DataBoundItem as RuleRow;
                if (row != null) { code.Text = row.FeeNo; type.SelectedIndex = row.CustomerType == "1" ? 0 : 1; }
            };
            grid.SelectionChanged += (s, e) => showSelected();
            grid.CellClick += (s, e) => showSelected();
            upsert.Click += (s, e) =>
            {
                try
                {
                    var rule = CustomerTypeRules.Validate(new[] { new KeyValuePair<string,string>(code.Text, type.SelectedIndex == 0 ? "1" : "2") }).Single();
                    var row = items.FirstOrDefault(r => r.FeeNo == rule.Key);
                    if (row == null) items.Add(new RuleRow { FeeNo = rule.Key, CustomerType = rule.Value }); else row.CustomerType = rule.Value;
                    items.ResetBindings();
                    var selected = items.First(r => r.FeeNo == rule.Key);
                    grid.CurrentCell = grid.Rows[items.IndexOf(selected)].Cells[0];
                    code.Clear(); code.Focus();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "请检查特例", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            remove.Click += (s, e) =>
            {
                var row = grid.CurrentRow?.DataBoundItem as RuleRow;
                if (row != null) items.Remove(row);
                code.Clear();
            };
            save.Click += (s, e) =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(code.Text))
                    {
                        var pending = CustomerTypeRules.Validate(new[] { new KeyValuePair<string,string>(code.Text, type.SelectedIndex == 0 ? "1" : "2") }).Single();
                        var existing = items.FirstOrDefault(r => r.FeeNo == pending.Key);
                        if (existing == null) items.Add(new RuleRow { FeeNo = pending.Key, CustomerType = pending.Value }); else existing.CustomerType = pending.Value;
                    }
                    var rules = CustomerTypeRules.Validate(items.Select(r => new KeyValuePair<string,string>(r.FeeNo, r.CustomerType)));
                    CustomerTypeRules.Save(ruleFile, rules); Rules = rules; DialogResult = DialogResult.OK; Close();
                }
                catch (Exception ex) { MessageBox.Show(this, "保存未完成：" + ex.Message, "保存特例失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
        }
    }
}
