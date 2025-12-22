using Avalonia;

namespace Virtualization.Avalonia;

internal class VirtualizationInfo
{
    public int Index { get; private set; } = -1;

    public bool IsPinned => _pinCounter > 0;

    public bool IsHeldByLayout => Owner is ElementOwner.Layout;

    public bool IsRealized => IsHeldByLayout || Owner is ElementOwner.PinnedPool;

    public bool IsInUniqueIdResetPool => Owner is ElementOwner.UniqueIdResetPool;

    public bool AutoRecycleCandidate { get; set; }

    public bool MustClearDataContext { get; set; }

    // In WinUI, this is a property on UIElement, but we don't have that
    // This is one solution (also could be an attached property, but that
    // would be an extra property read and possible perf issue)
    public bool CanBeScrollAnchor { get; set; }

    public ElementOwner Owner { get; private set; }

    public bool KeepAlive { get; set; }

    public Rect ArrangeBounds { get; set; } = ItemsRepeater.InvalidRect;

    public string? UniqueId { get; private set; }

    public object? Data => _data == null ? null : _data.TryGetTarget(out var target) ? target : null;
 
    internal void UpdatePhasingInfo(object data) => _data = new(data);

    internal void MoveOwnershipToLayoutFromElementFactory(int index, string uniqueId)
    {
        Owner = ElementOwner.Layout;
        Index = index;
        UniqueId = uniqueId;
    }

    internal void MoveOwnershipToLayoutFromUniqueIdResetPool()
    {
        Owner = ElementOwner.Layout;
    }

    internal void MoveOwnershipToLayoutFromPinnedPool()
    {
        Owner = ElementOwner.Layout;
    }

    internal void MoveOwnershipToElementFactory()
    {
        Owner = ElementOwner.ElementFactory;
        _pinCounter = 0;
        Index = -1;
        UniqueId = null;
        ArrangeBounds = ItemsRepeater.InvalidRect;
    }

    internal void MoveOwnershipToUniqueIdResetPoolFromLayout()
    {
        Owner = ElementOwner.UniqueIdResetPool;
        // Keep the pinCounter the same. If the container survives the reset
        // it can go on being pinned as if nothing happened.
    }

    internal void MoveOwnershipToAnimator()
    {
        // During a unique id reset, some elements might get removed.
        // Their ownership will go from the UniqueIdResetPool to the Animator.
        // The common path though is for ownership to go from Layout to Animator.
        Owner = ElementOwner.Animator;
        Index = -1;
        _pinCounter = 0;
    }

    internal void MoveOwnershipToPinnedPool()
    {
        Owner = ElementOwner.PinnedPool;
    }

    internal uint AddPin()
    {
        if (!IsRealized)
            throw new InvalidOperationException("You can't pin an unrealized element");

        ++_pinCounter;
        return ++_pinCounter;
    }

    internal uint RemovePin()
    {
        if (!IsRealized)
            throw new InvalidOperationException("You can't unpin an unrealized element");

        if (!IsPinned)
            throw new InvalidOperationException("UnpinElement was called more often than PinElement");

        --_pinCounter;
        return _pinCounter;
    }

    internal void UpdateIndex(int newIndex)
    {
        Index = newIndex;
    }

    private uint _pinCounter;

    private WeakReference<object>? _data;    

    internal const int PhaseReachedEnd = -1;

    public enum ElementOwner
    {
        ElementFactory,
        Layout,
        PinnedPool,
        UniqueIdResetPool,
        Animator
    }
}
