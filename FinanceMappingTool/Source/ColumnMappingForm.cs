using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FinanceMappingTool
{
    public sealed class ColumnMappingForm : Form
    {
        private readonly IReadOnlyList<ExcelColumn> columns;
        private readonly Dictionary<ExcelField, int> mapping;
        private readonly DataGridView fields = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White };
        private readonly CheckedListBox choices = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true,
            IntegralHeight = false, HorizontalScrollbar = true };
        private readonly Label title = new Label { AutoSize = true };
        private readonly TextBox sample = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private bool synchronizing = true;
        public Dictionary<ExcelField, int> Mapping => new Dictionary<ExcelField, int>(mapping);

        public ColumnMappingForm(IReadOnlyList<ExcelColumn> columns, IDictionary<ExcelField, int> initial)
        {
            this.columns = columns;
            mapping = new Dictionary<ExcelField, int>(initial);
            Text = "选择Excel字段对应关系";
            Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1060, 650); MinimumSize = new Size(900, 560);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 4, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(980, 0), Text =
                "表头有缺失、改名或重名，请先选择左侧所需字段，再勾选右侧Excel来源列。每个字段只能选一列，五个字段不可共用同一列。\r\n按列字母区分同名或空白表头；下方可查看当前来源列的示例值。已唯一匹配的字段会预先选好。" }, 0, 0);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            fields.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            fields.ColumnHeadersHeight = 40; fields.RowTemplate.Height = 48;
            fields.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "所需字段 / 用途", FillWeight = 60 });
            fields.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "已选Excel来源列", FillWeight = 40 });
            fields.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            foreach (var field in ExcelSchema.Fields)
                fields.Rows.Add(field.Header + "\r\n" + field.Description, SelectedLabel(field.Field));
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8, 0, 0, 0) };
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.Controls.Add(title, 0, 0); right.Controls.Add(choices, 0, 1);
            foreach (var column in columns) choices.Items.Add(column);
            body.Controls.Add(fields, 0, 0); body.Controls.Add(right, 1, 0); layout.Controls.Add(body, 0, 1);
            layout.Controls.Add(sample, 0, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "确认对应并读取", AutoSize = true };
            var cancel = new Button { Text = "取消导入", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(cancel); buttons.Controls.Add(ok); layout.Controls.Add(buttons, 0, 3);
            Controls.Add(layout); AcceptButton = ok; CancelButton = cancel;
            fields.CurrentCellChanged += (s, e) => ShowField();
            choices.SelectedIndexChanged += (s, e) => ShowSample();
            choices.ItemCheck += ChooseColumn;
            ok.Click += (s, e) =>
            {
                try { ExcelSchema.Validate(columns, mapping); DialogResult = DialogResult.OK; Close(); }
                catch (InvalidDataException ex) { MessageBox.Show(this, ex.Message, "请完成字段对应", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            synchronizing = false;
            fields.ClearSelection(); fields.Rows[0].Selected = true; fields.CurrentCell = fields.Rows[0].Cells[0]; ShowField();
        }

        private string SelectedLabel(ExcelField field)
        {
            int number;
            return mapping.TryGetValue(field, out number) ? columns.First(c => c.Number == number).Label : "（尚未选择）";
        }
        private int CurrentRow => fields.CurrentCell?.RowIndex ?? 0;
        private void ShowField()
        {
            if (synchronizing) return;
            var field = ExcelSchema.Fields[CurrentRow];
            title.Text = "请为“" + field.Header + "”勾选一个来源列：";
            synchronizing = true;
            try
            {
                int number; bool selected = mapping.TryGetValue(field.Field, out number);
                int selectedIndex = -1;
                for (int i = 0; i < columns.Count; i++)
                {
                    bool match = selected && columns[i].Number == number;
                    choices.SetItemChecked(i, match); if (match) selectedIndex = i;
                }
                choices.SelectedIndex = selectedIndex;
            }
            finally { synchronizing = false; }
            ShowSample();
        }
        private void ChooseColumn(object sender, ItemCheckEventArgs e)
        {
            if (synchronizing) return;
            int row = CurrentRow; var field = ExcelSchema.Fields[row].Field;
            if (e.NewValue == CheckState.Checked)
            {
                synchronizing = true;
                try { for (int i = 0; i < choices.Items.Count; i++) if (i != e.Index) choices.SetItemChecked(i, false); }
                finally { synchronizing = false; }
                mapping[field] = columns[e.Index].Number;
            }
            else
            {
                int selected;
                if (mapping.TryGetValue(field, out selected) && selected == columns[e.Index].Number) mapping.Remove(field);
            }
            fields.Rows[row].Cells[1].Value = SelectedLabel(field);
            choices.SelectedIndex = e.Index; ShowSample();
        }
        private void ShowSample()
        {
            int i = choices.SelectedIndex;
            sample.Text = i < 0 ? "请选择来源列以查看示例值。" : columns[i].Label + "\r\n前几行示例：" +
                (columns[i].Sample.Length == 0 ? "（前五行无非空值）" : columns[i].Sample);
        }
    }
}
