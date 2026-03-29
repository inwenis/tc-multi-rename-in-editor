using System.Drawing;

namespace TcBatchRename;

internal static class Ui
{
    public static void ShowError(string title, string message)
    {
        ShowMessageBox(
            title,
            message,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    public static void ShowInfo(string title, string message)
    {
        ShowMessageBox(
            title,
            message,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    public static bool ConfirmRename(RenamePlan plan)
    {
        using var form = new Form
        {
            Text = $"Confirm {plan.ChangedItems.Count} Rename(s)",
            StartPosition = FormStartPosition.CenterScreen,
            MinimumSize = new Size(900, 600),
            ClientSize = new Size(900, 600),
            TopMost = true
        };

        form.Shown += static (sender, _) =>
        {
            if (sender is Form shownForm)
            {
                shownForm.Activate();
                shownForm.BringToFront();
            }
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var messageLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "Review the pending renames. Click Rename to continue or Cancel to abort."
        };

        var previewBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9F),
            Text = string.Join(
                Environment.NewLine,
                plan.ChangedItems.Select(static item => $"{item.Source.FullPath}  ->  {item.NewName}"))
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var renameButton = new Button
        {
            Text = "Rename",
            DialogResult = DialogResult.OK,
            AutoSize = true
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };

        buttonPanel.Controls.Add(renameButton);
        buttonPanel.Controls.Add(cancelButton);

        layout.Controls.Add(messageLabel, 0, 0);
        layout.Controls.Add(previewBox, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);

        form.Controls.Add(layout);
        form.AcceptButton = renameButton;
        form.CancelButton = cancelButton;

        using var owner = CreateDialogOwner();
        owner.Show();
        owner.Activate();

        return form.ShowDialog(owner) == DialogResult.OK;
    }

    private static void ShowMessageBox(
        string title,
        string message,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        using var owner = CreateDialogOwner();
        owner.Show();
        owner.Activate();

        MessageBox.Show(
            owner,
            message,
            title,
            buttons,
            icon,
            MessageBoxDefaultButton.Button1);
    }

    private static Form CreateDialogOwner()
    {
        return new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Size = new Size(1, 1),
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            Opacity = 0,
            TopMost = true
        };
    }
}
