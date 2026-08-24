using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using VB6.Runtime;

namespace VB6.Runtime.WinForms;

[DefaultMember(nameof(Item))]
public sealed class TreeNodesProxy : IEnumerable<TreeNodeProxy>
{
    private readonly TreeView _treeView;

    internal TreeNodesProxy(TreeView treeView)
    {
        _treeView = treeView;
    }

    public int Count => EnumerateNodes().Count();

    public TreeNodeProxy? Item(object? index = null) =>
        FindNode(index) is { } node ? new TreeNodeProxy(_treeView, node) : null;

    public TreeNodeProxy Add(
        object? relative = null,
        object? relationship = null,
        object? key = null,
        object? text = null,
        object? image = null,
        object? selectedImage = null)
    {
        var relativeNode = FindNode(relative);
        var keyText = ToOptionalString(key);
        var node = new TreeNode(ToOptionalString(text) ?? string.Empty)
        {
            Name = keyText ?? string.Empty,
            ImageKey = ToOptionalString(image) ?? string.Empty,
            SelectedImageKey = ToOptionalString(selectedImage) ?? string.Empty
        };

        if (relativeNode is null)
        {
            _treeView.Nodes.Add(node);
        }
        else
        {
            var relationshipValue = ToOptionalLong(relationship);
            switch (relationshipValue)
            {
                case 4: // tvwChild
                    relativeNode.Nodes.Add(node);
                    break;
                case 0: // tvwFirst
                    (relativeNode.Parent?.Nodes ?? _treeView.Nodes).Insert(0, node);
                    break;
                case 1: // tvwLast
                    (relativeNode.Parent?.Nodes ?? _treeView.Nodes).Add(node);
                    break;
                case 2: // tvwNext
                    InsertRelative(relativeNode, node, 1);
                    break;
                case 3: // tvwPrevious
                    InsertRelative(relativeNode, node, 0);
                    break;
                default:
                    relativeNode.Nodes.Add(node);
                    break;
            }
        }

        return new TreeNodeProxy(_treeView, node);
    }

    public void Remove(object index)
    {
        var node = FindNode(index)
            ?? throw new ArgumentOutOfRangeException(nameof(index), "The TreeView node does not exist.");
        node.Remove();
    }

    public void Clear() => _treeView.Nodes.Clear();

    public IEnumerator<TreeNodeProxy> GetEnumerator() =>
        EnumerateNodes().Select(node => new TreeNodeProxy(_treeView, node)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void InsertRelative(TreeNode relative, TreeNode node, int offset)
    {
        var siblings = relative.Parent?.Nodes ?? _treeView.Nodes;
        siblings.Insert(Math.Min(relative.Index + offset, siblings.Count), node);
    }

    private TreeNode? FindNode(object? index)
    {
        if (index is null || VBVariants.IsMissing(index))
        {
            return null;
        }

        if (index is TreeNodeProxy proxy && ReferenceEquals(proxy.TreeView, _treeView))
        {
            return proxy.Node;
        }

        if (index is string key)
        {
            return _treeView.Nodes.Find(key, searchAllChildren: true).FirstOrDefault();
        }

        var numericIndex = checked(VBConversions.CLng(index));
        return numericIndex > 0
            ? EnumerateNodes().ElementAtOrDefault(numericIndex - 1)
            : null;
    }

    private IEnumerable<TreeNode> EnumerateNodes()
    {
        foreach (TreeNode node in _treeView.Nodes)
        {
            foreach (var descendant in EnumerateNode(node))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<TreeNode> EnumerateNode(TreeNode node)
    {
        yield return node;
        foreach (TreeNode child in node.Nodes)
        {
            foreach (var descendant in EnumerateNode(child))
            {
                yield return descendant;
            }
        }
    }

    private static string? ToOptionalString(object? value) =>
        value is null || VBVariants.IsMissing(value) || VBVariants.IsNull(value)
            ? null
            : VBConversions.CStr(value);

    private static long? ToOptionalLong(object? value) =>
        value is null || VBVariants.IsMissing(value) || VBVariants.IsNull(value)
            ? null
            : VBConversions.CLng(value);
}

public sealed class TreeNodeProxy
{
    private readonly TreeView _treeView;

    internal TreeNodeProxy(TreeView treeView, TreeNode node)
    {
        _treeView = treeView;
        Node = node;
    }

    internal TreeNode Node { get; }

    internal TreeView TreeView => _treeView;

    public string Key => Node.Name;

    public string Text
    {
        get => Node.Text;
        set => Node.Text = value ?? string.Empty;
    }

    public int Index => Flatten(_treeView).FindIndex(node => ReferenceEquals(node, Node)) + 1;

    public bool Expanded
    {
        get => Node.IsExpanded;
        set
        {
            if (value) Node.Expand();
            else Node.Collapse();
        }
    }

    public string Image
    {
        get => Node.ImageKey;
        set => Node.ImageKey = value ?? string.Empty;
    }

    public string SelectedImage
    {
        get => Node.SelectedImageKey;
        set => Node.SelectedImageKey = value ?? string.Empty;
    }

    public bool Selected
    {
        get => ReferenceEquals(_treeView.SelectedNode, Node);
        set
        {
            if (value) _treeView.SelectedNode = Node;
            else if (Selected) _treeView.SelectedNode = null;
        }
    }

    public TreeNodeProxy? Parent =>
        Node.Parent is { } parent ? new TreeNodeProxy(_treeView, parent) : null;

    public void Remove() => Node.Remove();

    private static List<TreeNode> Flatten(TreeView treeView)
    {
        var nodes = new TreeNodesProxy(treeView);
        return nodes.ToList().Select(proxy => proxy.Node).ToList();
    }
}
