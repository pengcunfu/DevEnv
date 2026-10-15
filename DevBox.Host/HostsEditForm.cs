using System.Text;

namespace DevBox.Host;

/// <summary>管理员态下编辑 Hosts（由 --edit-hosts 启动，绕过单实例）。</summary>
internal sealed class HostsEditForm : Form
{
    private static readonly string HostsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "drivers", "etc", "hosts");

    private readonly TextBox _editor = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10f),
        AcceptsTab = true,
        WordWrap = false,
    };

    public HostsEditForm()
    {
        Text = "Hosts 文件编辑器（管理员）";
        Width = 720;
        Height = 520;
        StartPosition = FormStartPosition.CenterScreen;

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.RightToLeft,
        };
        var save = new Button { Text = "保存", Width = 88, Height = 28 };
        var cancel = new Button { Text = "关闭", Width = 88, Height = 28 };
        save.Click += (_, _) => Save();
        cancel.Click += (_, _) => Close();
        bar.Controls.Add(cancel);
        bar.Controls.Add(save);

        var pathLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Text = HostsPath,
        };

        Controls.Add(_editor);
        Controls.Add(bar);
        Controls.Add(pathLabel);

        try
        {
            _editor.Text = File.Exists(HostsPath) ? File.ReadAllText(HostsPath, Encoding.UTF8) : "";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "读取失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(HostsPath, _editor.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            MessageBox.Show(this, "已保存", "Hosts", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
