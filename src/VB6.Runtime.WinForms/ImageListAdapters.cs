using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using VB6.Runtime;

namespace VB6.Runtime.WinForms;

public sealed class ImageListProxy
{
    public string Name { get; set; } = string.Empty;

    public int ImageWidth { get; set; }

    public int ImageHeight { get; set; }

    public long hImageList => 0;

    public ListImagesProxy ListImages { get; }

    public ImageListProxy()
    {
        ListImages = new ListImagesProxy();
    }
}

[DefaultMember(nameof(Item))]
public sealed class ListImagesProxy : IEnumerable<ListImageProxy>
{
    private readonly List<ListImageEntry> _entries = new();

    public int Count => _entries.Count;

    public ListImageProxy? Item(object? index = null) =>
        Find(index) is { } entry ? new ListImageProxy(this, entry) : null;

    public ListImageProxy Add(
        object? index = null,
        object? key = null,
        object? fileName = null)
    {
        var entry = new ListImageEntry
        {
            Key = ToOptionalString(key) ?? string.Empty,
            Picture = ToOptionalString(fileName) is { } path
                ? new VBPicture(path)
                : null
        };
        var insertionIndex = ToInsertionIndex(index, _entries.Count);
        _entries.Insert(insertionIndex, entry);
        return new ListImageProxy(this, entry);
    }

    public void Remove(object index)
    {
        var entry = Find(index)
            ?? throw new ArgumentOutOfRangeException(nameof(index), "The ImageList entry does not exist.");
        _entries.Remove(entry);
    }

    public void Clear() => _entries.Clear();

    public IEnumerator<ListImageProxy> GetEnumerator() =>
        _entries.Select(entry => new ListImageProxy(this, entry)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal int IndexOf(ListImageEntry entry) => _entries.IndexOf(entry) + 1;

    private ListImageEntry? Find(object? index)
    {
        if (index is null || VBVariants.IsMissing(index) || VBVariants.IsNull(index))
        {
            return null;
        }

        if (index is ListImageProxy proxy)
        {
            return ReferenceEquals(proxy.Owner, this) ? proxy.Entry : null;
        }

        if (index is string key)
        {
            return _entries.FirstOrDefault(entry =>
                string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        var numericIndex = VBConversions.CLng(index);
        return numericIndex > 0 && numericIndex <= _entries.Count
            ? _entries[numericIndex - 1]
            : null;
    }

    private static int ToInsertionIndex(object? index, int count)
    {
        if (index is null || VBVariants.IsMissing(index) || VBVariants.IsNull(index))
        {
            return count;
        }

        return Math.Clamp(VBConversions.CLng(index) - 1, 0, count);
    }

    private static string? ToOptionalString(object? value) =>
        value is null || VBVariants.IsMissing(value) || VBVariants.IsNull(value)
            ? null
            : VBConversions.CStr(value);

    internal sealed class ListImageEntry
    {
        public string Key { get; set; } = string.Empty;

        public object? Picture { get; set; }
    }
}

public sealed class ListImageProxy
{
    private readonly ListImagesProxy _owner;

    internal ListImageProxy(ListImagesProxy owner, ListImagesProxy.ListImageEntry entry)
    {
        _owner = owner;
        Entry = entry;
    }

    internal ListImagesProxy Owner => _owner;

    internal ListImagesProxy.ListImageEntry Entry { get; }

    public string Key
    {
        get => Entry.Key;
        set => Entry.Key = value ?? string.Empty;
    }

    public int Index => _owner.IndexOf(Entry);

    public object? Picture
    {
        get => Entry.Picture;
        set => Entry.Picture = value;
    }
}

public sealed class ImageComboControl : ComboBox
{
    internal List<ComboItemEntry> Entries { get; } = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? ImageList { get; set; }
}

[DefaultMember(nameof(Item))]
public sealed class ComboItemsProxy : IEnumerable<ComboItemProxy>
{
    private readonly ImageComboControl _control;

    internal ComboItemsProxy(ImageComboControl control)
    {
        _control = control;
    }

    public int Count => _control.Entries.Count;

    public ComboItemProxy? Item(object? index = null) =>
        Find(index) is { } entry ? new ComboItemProxy(_control, entry) : null;

    public ComboItemProxy Add(
        object? index = null,
        object? key = null,
        object? text = null,
        object? image = null)
    {
        var entry = new ComboItemEntry
        {
            Key = ToOptionalString(key) ?? string.Empty,
            Text = ToOptionalString(text) ?? string.Empty,
            Image = ToOptionalLong(image) ?? 0
        };
        var insertionIndex = ToInsertionIndex(index, _control.Entries.Count);
        _control.Entries.Insert(insertionIndex, entry);
        _control.Items.Insert(insertionIndex, entry.Text);
        return new ComboItemProxy(_control, entry);
    }

    public void Remove(object index)
    {
        var entry = Find(index)
            ?? throw new ArgumentOutOfRangeException(nameof(index), "The ImageCombo entry does not exist.");
        var itemIndex = _control.Entries.IndexOf(entry);
        _control.Entries.RemoveAt(itemIndex);
        _control.Items.RemoveAt(itemIndex);
    }

    public void Clear()
    {
        _control.Entries.Clear();
        _control.Items.Clear();
    }

    public IEnumerator<ComboItemProxy> GetEnumerator() =>
        _control.Entries.Select(entry => new ComboItemProxy(_control, entry)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private ComboItemEntry? Find(object? index)
    {
        if (index is null || VBVariants.IsMissing(index) || VBVariants.IsNull(index))
        {
            return null;
        }

        if (index is ComboItemProxy proxy)
        {
            return ReferenceEquals(proxy.Control, _control) ? proxy.Entry : null;
        }

        if (index is string key)
        {
            return _control.Entries.FirstOrDefault(entry =>
                string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        var numericIndex = VBConversions.CLng(index);
        return numericIndex > 0 && numericIndex <= _control.Entries.Count
            ? _control.Entries[numericIndex - 1]
            : null;
    }

    private static int ToInsertionIndex(object? index, int count)
    {
        if (index is null || VBVariants.IsMissing(index) || VBVariants.IsNull(index))
        {
            return count;
        }

        return Math.Clamp(VBConversions.CLng(index) - 1, 0, count);
    }

    private static string? ToOptionalString(object? value) =>
        value is null || VBVariants.IsMissing(value) || VBVariants.IsNull(value)
            ? null
            : VBConversions.CStr(value);

    private static int? ToOptionalLong(object? value) =>
        value is null || VBVariants.IsMissing(value) || VBVariants.IsNull(value)
            ? null
            : VBConversions.CLng(value);
}

public sealed class ComboItemProxy
{
    private readonly ImageComboControl _control;

    internal ComboItemProxy(ImageComboControl control, ComboItemEntry entry)
    {
        _control = control;
        Entry = entry;
    }

    internal ImageComboControl Control => _control;

    internal ComboItemEntry Entry { get; }

    public string Key => Entry.Key;

    public int Index => _control.Entries.IndexOf(Entry) + 1;

    public string Text
    {
        get => Entry.Text;
        set
        {
            Entry.Text = value ?? string.Empty;
            var index = _control.Entries.IndexOf(Entry);
            if (index >= 0) _control.Items[index] = Entry.Text;
        }
    }

    public bool Selected
    {
        get => _control.Entries.IndexOf(Entry) == _control.SelectedIndex;
        set
        {
            var index = _control.Entries.IndexOf(Entry);
            if (index >= 0 && value) _control.SelectedIndex = index;
            else if (index == _control.SelectedIndex && !value) _control.SelectedIndex = -1;
        }
    }

    public int Image
    {
        get => Entry.Image;
        set => Entry.Image = value;
    }
}

public sealed class ComboItemEntry
{
    public string Key { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public int Image { get; set; }
}
