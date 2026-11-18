using System;
using System.Drawing;
using System.Windows.Forms;
namespace FinanceMappingTool
{
    public sealed class ReferenceConnectionForm : Form
    {
        private readonly TextBox server = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox database = new TextBox { Dock = DockStyle.Fill };
        private readonly CheckBox windows = new CheckBox { Text = "使用 Windows 身份验证", AutoSize = true };
        private readonly TextBox user = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox password = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        private readonly CheckBox encrypt = new CheckBox { Text = "加密连接", AutoSize = true };
        private readonly CheckBox trust = new CheckBox { Text = "信任服务器证书（仅在确认服务器可信时启用）", AutoSize = true };
        public ReferenceConnectionSettings Settings => new ReferenceConnectionSettings { Server = server.Text.Trim(),
            Database = database.Text.Trim(), WindowsAuthentication = windows.Checked, User = user.Text.Trim(), Password = password.Text,
            Encrypt = encrypt.Checked, TrustServerCertificate = trust.Checked };
        public ReferenceConnectionForm(ReferenceConnectionSettings current)
        {
            Text = "读取客户/供应商参照数据"; Font = new Font("Microsoft YaHei UI", 9F); AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ClientSize = new Size(580, 350);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 9 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var info = new Label { AutoSize = true, MaximumSize = new Size(545, 0), Text =
                "仅查询有效 KDVoucher 的 FeeNo 和 CustomerType（1客户，2供应商）。不会向数据库写入数据。连接信息仅用于本机本次读取，密码不保存。" };
            layout.Controls.Add(info, 0, 0); layout.SetColumnSpan(info, 2);
            Add(layout, "SQL Server", server, 1); Add(layout, "参照数据库", database, 2);
            layout.Controls.Add(windows, 1, 3); Add(layout, "SQL 登录名", user, 4); Add(layout, "密码", password, 5);
            var security = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            security.Controls.Add(encrypt); security.Controls.Add(trust); layout.Controls.Add(security, 1, 6);
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
            var ok = new Button { Text = "读取参照并导出", AutoSize = true };
            buttons.Controls.Add(cancel); buttons.Controls.Add(ok); layout.Controls.Add(buttons, 0, 8); layout.SetColumnSpan(buttons, 2);
            for (int i = 0; i < 9; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout); AcceptButton = ok; CancelButton = cancel;
            server.Text = current.Server; database.Text = current.Database; windows.Checked = current.WindowsAuthentication;
            user.Text = current.User; encrypt.Checked = current.Encrypt; trust.Checked = current.TrustServerCertificate;
            user.Enabled = password.Enabled = !windows.Checked;
            windows.CheckedChanged += (s, e) => user.Enabled = password.Enabled = !windows.Checked;
            ok.Click += (s, e) =>
            {
                if (server.Text.Trim().Length == 0 || database.Text.Trim().Length == 0 || (!windows.Checked && user.Text.Trim().Length == 0))
                { MessageBox.Show(this, "请填写服务器、参照数据库，以及SQL身份验证所需的登录名。", "连接信息不完整"); return; }
                DialogResult = DialogResult.OK; Close();
            };
        }
        private static void Add(TableLayoutPanel p, string text, Control control, int row)
        { p.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, row); p.Controls.Add(control, 1, row); }
    }
}
