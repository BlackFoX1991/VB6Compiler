using System.Windows.Forms;

namespace VB6.Runtime.WinForms;

/// <summary>
/// Managed adapter for the frequently used MSComDlg.CommonDialog surface. It is deliberately a
/// component rather than a visual Control, so designer instances do not alter the form layout.
/// </summary>
public sealed class CommonDialogProxy
{
    public string Name { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Filter { get; set; } = string.Empty;

    public string DialogTitle { get; set; } = string.Empty;

    public int FilterIndex { get; set; } = 1;

    public bool CancelError { get; set; }

    public string DefaultExt { get; set; } = string.Empty;

    public void ShowOpen() => Show(save: false);

    public void ShowSave() => Show(save: true);

    private void Show(bool save)
    {
        using FileDialog dialog = save ? new SaveFileDialog() : new OpenFileDialog();
        dialog.FileName = FileName;
        dialog.Filter = string.IsNullOrWhiteSpace(Filter) ? "All files (*.*)|*.*" : Filter;
        dialog.FilterIndex = Math.Max(1, FilterIndex);
        dialog.Title = DialogTitle;
        if (dialog is SaveFileDialog saveDialog && !string.IsNullOrWhiteSpace(DefaultExt))
        {
            saveDialog.DefaultExt = DefaultExt;
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            FileName = dialog.FileName;
            FilterIndex = dialog.FilterIndex;
            return;
        }

        if (CancelError)
        {
            throw new OperationCanceledException("The CommonDialog operation was cancelled.");
        }
    }
}
