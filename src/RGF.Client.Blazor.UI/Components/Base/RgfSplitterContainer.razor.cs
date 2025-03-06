using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;

public enum RgfSplitterDirection
{
    None,
    Left,
    Right,
    Top,
    Bottom
}

public class RgfSplitterNode
{
    private static int _nextId = 1;

    public int Id { get; private set; }

    public RgfSplitterNode? Parent { get; private set; }

    public RenderFragment? Content { get; internal set; }

    public RgfSplitterDirection Direction { get; private set; }

    public RgfSplitterNode? PrimaryNode { get; private set; }

    public RgfSplitterNode? SecondaryNode { get; private set; }

    public bool IsDeleted { get; private set; }

    internal RgfSplitterNode(RgfSplitterNode? parent = null)
    {
        Id = _nextId++;
        Parent = parent;
        Direction = RgfSplitterDirection.None;
    }

    internal RgfSplitterNode(RenderFragment content, RgfSplitterNode? parent = null) : this(parent)
    {
        Content = content;
    }

    internal static RgfSplitterNode CreateRoot(RenderFragment? content = null) => new RgfSplitterNode(content ?? ((b) => { }));

    public RgfSplitterNode Split(RenderFragment newContent, RgfSplitterDirection splitDirection = RgfSplitterDirection.Right)
    {
        if (Direction != RgfSplitterDirection.None)
        {
            if (Direction == RgfSplitterDirection.Right || Direction == RgfSplitterDirection.Bottom)
            {
                if (PrimaryNode != null)
                {
                    return PrimaryNode.Split(newContent, splitDirection);
                }
                PrimaryNode = new RgfSplitterNode(newContent, this);
                return PrimaryNode;
            }

            if (SecondaryNode != null)
            {
                return SecondaryNode.Split(newContent, splitDirection);
            }
            SecondaryNode = new RgfSplitterNode(newContent, this);
            return SecondaryNode;
        }

        if (splitDirection == RgfSplitterDirection.None)
        {
            throw new InvalidOperationException("Invalid direction");
        }

        var newSplitter = new RgfSplitterNode(Parent)
        {
            Direction = splitDirection
        };

        if (Parent?.PrimaryNode == this)
        {
            Parent.PrimaryNode = newSplitter;
        }
        else if (Parent?.SecondaryNode == this)
        {
            Parent.SecondaryNode = newSplitter;
        }

        Parent = newSplitter;
        if (splitDirection == RgfSplitterDirection.Right || splitDirection == RgfSplitterDirection.Bottom)
        {
            newSplitter.PrimaryNode = this;
            newSplitter.SecondaryNode = new RgfSplitterNode(newContent, newSplitter);
            return newSplitter.SecondaryNode;
        }

        newSplitter.SecondaryNode = this;
        newSplitter.PrimaryNode = new RgfSplitterNode(newContent, newSplitter);
        return newSplitter.PrimaryNode;
    }

    public bool Remove(ILogger logger, RgfSplitterNode? initialNode)
    {
        //keep the node if it is the initial node
        if (Id == initialNode?.Id)
        {
            logger.LogDebug("Id {Id} was not removed. Keeping initial.", Id);
            return false;
        }

        // during splitting, the initial always moves down
        if (initialNode == null || FindNode(initialNode?.Id) == null)
        {
            if (Parent == null)
            {
                logger.LogDebug("Removed Root Id: {Id}", Id);
                IsDeleted = true;
                return true;
            }

            logger.LogDebug("Removed Id: {Id}", Id);
            IsDeleted = true;

            if (Parent.PrimaryNode == this)
            {
                Parent.PrimaryNode = null;
            }
            else
            {
                if (Parent.SecondaryNode != this) throw new InvalidOperationException("Hierarchy error 1");
                Parent.SecondaryNode = null;
            }
            Parent.OptimizeHierarchy(logger, initialNode);
            return true;
        }

        while (initialNode!.Parent != this)
        {
            if (initialNode.Parent == null) throw new InvalidOperationException("Hierarchy error 2");
            initialNode.Parent.Remove(logger, initialNode);
        }

        if (Parent == null)
        {
            //Replace root node with initial node
            initialNode.Parent = null;
            logger.LogDebug("Replace Root with Id: {Id}", initialNode.Id);
            IsDeleted = true;
            return true;
        }

        if (Parent.PrimaryNode == this)
        {
            Parent.PrimaryNode = initialNode;
        }
        else
        {
            if (Parent.SecondaryNode != this) throw new InvalidOperationException("Hierarchy error 3");
            Parent.SecondaryNode = initialNode;
        }
        initialNode.Parent = Parent;
        logger.LogDebug("Removed T Id: {Id}", Id);
        IsDeleted = true;
        Parent.OptimizeHierarchy(logger, initialNode);
        return true;
    }

    private void OptimizeHierarchy(ILogger logger, RgfSplitterNode? initialNode)
    {
        if (PrimaryNode != null && SecondaryNode != null)
        {
            return;
        }

        if (PrimaryNode == null && SecondaryNode == null)
        {
            if (Content == null)
            {
                Remove(logger, initialNode);
            }
            Parent?.OptimizeHierarchy(logger, initialNode);
            return;
        }

        if (Parent == null)
        {
            if (PrimaryNode?.IsDeleted == false)
            {
                PrimaryNode.Parent = null;
                IsDeleted = true;
                logger.LogDebug("Optimize no parent. Root: {Id}", PrimaryNode.Id);
            }
            else if (SecondaryNode!.IsDeleted == false)
            {
                SecondaryNode.Parent = null;
                IsDeleted = true;
                logger.LogDebug("Optimize no parent. Root: {Id}", SecondaryNode.Id);
            }
        }
        else
        {
            if (Parent.PrimaryNode == this)
            {
                Parent.PrimaryNode = PrimaryNode ?? SecondaryNode ?? throw new InvalidOperationException("Hierarchy error OP1");
                Parent.PrimaryNode.Parent = Parent;
            }
            else
            {
                if (Parent.SecondaryNode != this) throw new InvalidOperationException("Hierarchy error OP2");
                Parent.SecondaryNode = PrimaryNode ?? SecondaryNode ?? throw new InvalidOperationException("Hierarchy error OP3");
                Parent.SecondaryNode.Parent = Parent;
            }

            logger.LogDebug("Removed O Id: {Id}", Id);
            IsDeleted = true;
            Parent?.OptimizeHierarchy(logger, initialNode);
        }
    }

    public RgfSplitterNode? FindNode(int? id) => id == null ? null : id == Id ? this : PrimaryNode?.FindNode(id) ?? SecondaryNode?.FindNode(id);
}

public partial class RgfSplitterContainer
{
    [Inject]
    private ILogger<RgfSplitterContainer> _logger { get; set; } = null!;

    [Parameter]
    public RgfSplitterContainer? ParentContainer { get; set; }

    [Parameter]
    public RgfSplitterNode? Node { get; set; }

    internal int Level { get; set; } = 0;

    //[CascadingParameter]
    internal RgfSplitterNode? InitialNode { get; set; }

    protected List<RgfSplitterNode> DisplayedNodes { get; set; } = [];

    private RgfSplitterNode? GetRoot(RgfSplitterNode? node) => node?.Parent == null ? node : GetRoot(node.Parent);

    private RgfSplitterNode? FindNode(int id) => DisplayedNodes.FirstOrDefault(n => n.Id == id);

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (ParentContainer != null)
        {
            Level = ParentContainer.Level + 1;
            InitialNode = ParentContainer.InitialNode;
        }
        else if (InitialContent != null)
        {
            if (InitialNode?.IsDeleted != false)
            {
                InitialNode = RgfSplitterNode.CreateRoot(InitialContent);
                DisplayedNodes.Add(InitialNode);
                _logger.LogDebug("Init Root Id:{Id}", InitialNode.Id);
            }
            else
            {
                InitialNode.Content = InitialContent;
                _logger.LogDebug("Initial content set: {id}", InitialNode.Id);
            }
            Node = GetRoot(InitialNode);
        }
    }

    public bool IsNodeValid(int id) => FindNode(id)?.IsDeleted == false;

    public int? CreateNode(RenderFragment content, RgfSplitterDirection direction, int? parentId = null)
    {
        RgfSplitterNode node;
        var root = GetRoot(InitialNode);
        if (root == null)
        {
            node = InitialNode = new RgfSplitterNode(content);
        }
        else
        {
            var parent = parentId == null || parentId == 0 ? root : FindNode((int)parentId);
            if (parent == null)
            {
                _logger.LogError("Parent node ({Id}) not found", parentId);
                return null;
            }
            node = parent.Split(content, direction);
        }

        DisplayedNodes.Add(node);
        Node = GetRoot(node);
        _logger.LogDebug("Node created. Id: {Id}, Parent: {ParentId}, Level: {Level}", node.Id, node.Parent?.Id, Level);
        StateHasChanged();
        return node.Id;
    }

    public int? CreateNode(RgfDialogParameters dialogParameters, RgfSplitterDirection direction, int? parentId = null)
    {
        dialogParameters.IsInline = true;
        RenderFragment content = (builder) =>
        {
            int sequence = 0;
            builder.OpenComponent(sequence++, typeof(DialogComponent));
            builder.AddAttribute(sequence++, nameof(DialogComponent.DialogParameters), dialogParameters);
            builder.CloseComponent();
        };
        var nodeId = CreateNode(content, direction, parentId);
        var self = this;
        dialogParameters.EventDispatcher.Subscribe(RgfDialogEventKind.Destroy, (args) =>
        {
            if (nodeId != null) RemoveNode((int)nodeId);
            dialogParameters.EventDispatcher.Unsubscribe(self);
        }, self);
        return nodeId;
    }

    public bool RemoveNode(int id)
    {
        var node = FindNode(id);
        if (node == null)
        {
            _logger.LogError("Node {Id} not found", id);
            return false;
        }

        bool result = node.Remove(_logger, InitialContent == null ? null : InitialNode);
        if (result)
        {
            DisplayedNodes.Remove(node);
            Node = GetRoot(DisplayedNodes.FirstOrDefault());
            if (node == InitialNode)
            {
                InitialNode = Node;
            }
            StateHasChanged();
        }
        return result;
    }
}