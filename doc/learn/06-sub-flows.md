# 6. Sub-flows and groups

*React Flow equivalent: Sub Flows.*

A node can contain other nodes: it is a **group** (type `group`) and its **children**.

```csharp
Flow.Nodes =
[
    new Node("g1", new XYPosition(0, 0), "group", "Group A", FixedWidth: 360, FixedHeight: 230),

    // The positions of the children are RELATIVE to the parent.
    new Node("c1", new XYPosition(20, 40),  "default", "Child 1", ParentId: "g1", ExtentParent: true),
    new Node("c2", new XYPosition(180, 130), "default", "Child 2", ParentId: "g1", ExtentParent: true),

    new Node("free", new XYPosition(520, 70), "output", "Outside the group"),
];
Flow.Edges = [ new Edge("e1", "c1", "c2"), new Edge("e2", "c2", "free") ];
```

| Property | Effect |
|---|---|
| `ParentId` | Makes this node the child of the node carrying this identifier. `Position` becomes relative to the parent's corner. |
| `ExtentParent` | The child cannot leave its parent (equivalent of `extent: 'parent'`). |
| `FixedWidth` / `FixedHeight` | Size of the group. |

## Behaviours

- **Moving** a parent moves all its descendants; if parent and child are selected together, the child is not moved twice.
- **Deleting** a parent deletes its descendants (and their edges).
- **Stacking**: a parent is displayed below its children (the z-index follows the depth). A selected group does not come in front of its children.
- Edges connect any nodes, inside or outside a group; they are drawn in the absolute coordinate space.
- Deep nesting (grandchildren…) is supported. An unknown `ParentId` or a cycle makes the node be treated as a root.

## Appearance

The built-in `group` type is a translucent area with a thin border (keys `Flow.GroupBackground` and `Flow.NodeBorder`), with `Data` displayed as its title at the top left. For a custom group, define a `DataTemplate` keyed `wpflow-node-group`, or use another type: any node can have children as long as it has a size.

## Core side

`NodeTree` exposes the calculations, usable without any UI:

```csharp
var abs = NodeTree.AbsolutePositions(nodes);           // Id -> absolute position
var depths = NodeTree.Depths(nodes);                   // Id -> depth
var all = NodeTree.Descendants("g1", nodes);           // all descendants
var clamped = NodeTree.ClampToParent(child, wanted, parent);
```

`GraphUtils.GetNodesBounds` also works in absolute coordinates.

Next: [Automatic layout](07-layouting.md).
