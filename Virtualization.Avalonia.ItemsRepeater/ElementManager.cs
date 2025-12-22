using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Virtualization.Avalonia.Layouts;


namespace Virtualization.Avalonia;

internal enum ScrollOrientation
{
    Horizontal,
    Vertical
}

internal class ElementManager(bool useLayoutBounds = true)
{
    public int FirstRealizedIndex => _firstRealizedDataIndex;

    public int LastRealizedIndex => _firstRealizedDataIndex + _realizedElements.Count - 1;

    public void SetContext(VirtualizingLayoutContext virtualContext)
    {
        _context = virtualContext;
    }

    public void OnBeginMeasure(ScrollOrientation orientation)
    {
        if (!ContextNotNull)
            return;

        if (IsVirtualizingContext())
        {
            // We proactively clear elements laid out outside of the realizaton
            // rect so that they are available for reuse during the current
            // measure pass.
            // This is useful during fast panning scenarios in which the realization
            // window is constantly changing and we want to reuse elements from
            // the end that's opposite to the panning direction.
            DiscardElementsOutsideWindow(_context.RealizationRect, orientation);
        }
        else
        {
            // If we are initialized with a non-virtualizing context, make sure that
            // we have enough space to hold the bounds for all the elements.
            var count = _context.ItemsCount;
            if (_realizedElementLayoutBounds.Count != count)
            {
                // Make sure there is enough space for the bounds.
                // Note: We could optimize when the count becomes smaller, but keeping
                // it always up to date is the simplest option for now.
                _realizedElementLayoutBounds.Capacity = count;
            }
        }
    }

    public int GetRealizedElementCount() =>
        IsVirtualizingContext() ? _realizedElements.Count : _context.ItemsCount;

    public Control GetAt(int realizedIndex)
    {
        if (IsVirtualizingContext())
        {
            var element = _realizedElements[realizedIndex];
            if (element is null)
            {
                // Sentinel. Create the element now since we need it.
                var dataIndex = GetDataIndexFromRealizedRangeIndex(realizedIndex);
#if DEBUG && REPEATER_TRACE
                Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this,"Creating element for sentinal with data index {Index}", dataIndex);
#endif
                element = _context.GetOrCreateElementAt(dataIndex,
                    ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
                _realizedElements[realizedIndex] = element;
            }
            return element;
        }

        return _context.GetOrCreateElementAt(realizedIndex,
            ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
    }

    public void Add(Control element, int dataIndex)
    {
        Debug.Assert(element is not null);
        Debug.Assert(IsVirtualizingContext());

        if (_realizedElements.Count == 0)
            _firstRealizedDataIndex = dataIndex;

        _realizedElements.Add(element);

        if (useLayoutBounds)
        {
            _realizedElementLayoutBounds.Add(default);
        }
    }

    public void Insert(int realizedIndex, int dataIndex, Control? element)
    {
        Debug.Assert(IsVirtualizingContext());
        if (realizedIndex == 0)
        {
            _firstRealizedDataIndex = dataIndex;
        }

        _realizedElements.Insert(realizedIndex, element);

        if (useLayoutBounds)
        {
            _realizedElementLayoutBounds.Insert(realizedIndex, new Rect(-1, -1, -1, -1));
        }
    }

    public void ClearRealizedRange(int realizedIndex, int count)
    {
        Debug.Assert(IsVirtualizingContext());

        for (var i = 0; i < count; i++)
        {
            // Clear from the edges so that ItemsRepeater can optimize on maintaining 
            // realized indices without walking through all the children every time.
            var index = realizedIndex == 0 ? realizedIndex + i : (realizedIndex + count - 1) - i;
            if (_realizedElements[index] is { } c)
            {
                _context.RecycleElement(c);
            }
        }

        var endIndex = realizedIndex + count;
        _realizedElements.RemoveRange(realizedIndex, endIndex - realizedIndex);

        if (useLayoutBounds)
        {
            _realizedElementLayoutBounds.RemoveRange(realizedIndex, endIndex - realizedIndex);
        }


        if (realizedIndex == 0)
        {
            _firstRealizedDataIndex = _realizedElements.Count == 0 ? 
                -1 : _firstRealizedDataIndex + count;
        }
    }

    public void DiscardElementsOutsideWindow(bool forward, int startIndex)
    {
        // Remove layout elements that are outside the realized range.
        if (IsDataIndexRealized(startIndex))
        {
            Debug.Assert(IsVirtualizingContext());
            var rangeIndex = GetRealizedRangeIndexFromDataIndex(startIndex);

            if (forward)
            {
                ClearRealizedRange(rangeIndex, GetRealizedElementCount() - rangeIndex);
            }
            else
            {
                ClearRealizedRange(0, rangeIndex + 1);
            }
        }
    }

    public void ClearRealizedRange()
    {
        Debug.Assert(IsVirtualizingContext());
        ClearRealizedRange(0, GetRealizedElementCount());
    }

    public Rect GetLayoutBoundsForDataIndex(int dataIndex)
    {
        var realizedIndex = GetRealizedRangeIndexFromDataIndex(dataIndex);
        return _realizedElementLayoutBounds[realizedIndex];
    }

    public void SetLayoutBoundsForDataIndex(int dataIndex, Rect bounds)
    {
        var realizedIndex = GetRealizedRangeIndexFromDataIndex(dataIndex);
        _realizedElementLayoutBounds[realizedIndex] = bounds;
    }

    public Rect GetLayoutBoundsForRealizedIndex(int realizedIndex) =>
        _realizedElementLayoutBounds[realizedIndex];

    public void SetLayoutBoundsForRealizedIndex(int realizedIndex, Rect bounds) =>
        _realizedElementLayoutBounds[realizedIndex] = bounds;

    [MemberNotNull(nameof(_context))]
    public bool IsDataIndexRealized(int index)
    {
        if (IsVirtualizingContext())
        {
            var realizedCount = GetRealizedElementCount();
            return realizedCount > 0 &&
                GetDataIndexFromRealizedRangeIndex(0) <= index &&
                GetDataIndexFromRealizedRangeIndex(realizedCount - 1) >= index;
        }
        else
        {
            // Non virtualized - everything is realized
            return index >= 0 && index < _context.ItemsCount;
        }
    }

    public bool IsIndexValidInData(int currentIndex)
    {
        Debug.Assert(ContextNotNull);
        return currentIndex >= 0 && currentIndex < _context.ItemsCount;
    }

    public Control GetRealizedElement(int dataIndex)
    {
        Debug.Assert(IsDataIndexRealized(dataIndex));
        return IsVirtualizingContext() ?
            GetAt(GetRealizedRangeIndexFromDataIndex(dataIndex)) :
            _context.GetOrCreateElementAt(dataIndex,
                ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
    }

    public void EnsureElementRealized(bool forward, int dataIndex)
    {
        if (IsDataIndexRealized(dataIndex))
            return;
        var element = _context.GetOrCreateElementAt(dataIndex,
            ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);

        if (forward)
        {
            Add(element, dataIndex);
        }
        else
        {
            Insert(0, dataIndex, element);
        }

        Debug.Assert(IsDataIndexRealized(dataIndex));
#if DEBUG && REPEATER_TRACE
            Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this,"Created element for index {Index}", dataIndex);
#endif
    }

    // Does the given window intersect the range of realized elements
    public bool IsWindowConnected(Rect window, ScrollOrientation orientation, 
        bool scrollOrientationSameAsFlow)
    {
        Debug.Assert(IsVirtualizingContext());
        Debug.Assert(useLayoutBounds);

        var intersects = false;
        if (_realizedElementLayoutBounds.Count > 0)
        {
            var firstElementBounds = GetLayoutBoundsForRealizedIndex(0);
            var lastElementBounds = GetLayoutBoundsForRealizedIndex(GetRealizedElementCount() - 1);

            var effectiveOrientation = scrollOrientationSameAsFlow ?
                (orientation == ScrollOrientation.Vertical ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical) :
                orientation;

            var windowStart = effectiveOrientation == ScrollOrientation.Vertical ? 
                window.Y : window.X;
            var windowEnd = effectiveOrientation == ScrollOrientation.Vertical ? 
                window.Y + window.Height : window.X + window.Width;
            var firstElementStart = effectiveOrientation == ScrollOrientation.Vertical ? 
                firstElementBounds.Y : firstElementBounds.X;
            var lastElementEnd = effectiveOrientation == ScrollOrientation.Vertical ? 
                lastElementBounds.Y + lastElementBounds.Height : lastElementBounds.X + lastElementBounds.Width;

            intersects = firstElementStart <= windowEnd &&
                lastElementEnd >= windowStart;
        }

        return intersects;
    }

    public void DataSourceChanged(object? source, NotifyCollectionChangedEventArgs args)
    {
        Debug.Assert(IsVirtualizingContext());
        if (_realizedElements.Count == 0)
            return;

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                OnItemsAdded(args.NewStartingIndex, args.NewItems!.Count);
                break;

            case NotifyCollectionChangedAction.Replace:
                {
                    var oldSize = args.OldItems!.Count;
                    var newSize = args.NewItems!.Count;
                    var oldStartIndex = args.OldStartingIndex;
                    var newStartIndex = args.NewStartingIndex;

                    if (oldSize == newSize &&
                        oldStartIndex == newStartIndex &&
                        IsDataIndexRealized(oldStartIndex) &&
                        IsDataIndexRealized(oldStartIndex + oldSize - 1))
                    {
                        // Straight up replace of n items within the realization window.
                        // Removing and adding might causes us to lose the anchor causing us
                        // to throw away all containers and start from scratch.
                        // Instead, we can just clear those items and set the element to
                        // null (sentinel) and let the next measure get new containers for them.
                        var startRealizedIndex = GetRealizedRangeIndexFromDataIndex(oldStartIndex);
                        for (var realizedIndex = startRealizedIndex; realizedIndex < startRealizedIndex + oldSize; realizedIndex++)
                        {
                            if (_realizedElements[realizedIndex] is not { } c)
                                continue;
                            _context.RecycleElement(c);
                            _realizedElements[realizedIndex] = null;
                        }
                    }
                    else
                    {
                        OnItemsRemoved(oldStartIndex, oldSize);
                        OnItemsAdded(newStartIndex, newSize);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                OnItemsRemoved(args.OldStartingIndex, args.OldItems!.Count);
                break;

            case NotifyCollectionChangedAction.Reset:
                ClearRealizedRange();
                break;

            case NotifyCollectionChangedAction.Move:
                var size = args.OldItems?.Count ?? 1;
                OnItemsRemoved(args.OldStartingIndex, size);
                OnItemsAdded(args.NewStartingIndex, size);
                break;
        }
    }

    public int GetElementDataIndex(Control suggestedAnchor)
    {
        Debug.Assert(suggestedAnchor != null);
        var idx = _realizedElements.IndexOf(suggestedAnchor);
        return idx != -1 ?
            GetDataIndexFromRealizedRangeIndex(idx) :
            -1;
    }

    public int GetDataIndexFromRealizedRangeIndex(int rangeIndex)
    {
        Debug.Assert(rangeIndex >= 0 && rangeIndex < GetRealizedElementCount());
        return IsVirtualizingContext() ? rangeIndex + _firstRealizedDataIndex : rangeIndex;
    }

    public int GetRealizedRangeIndexFromDataIndex(int dataIndex)
    {
        Debug.Assert(IsDataIndexRealized(dataIndex));
        return IsVirtualizingContext() ? dataIndex - _firstRealizedDataIndex : dataIndex;
    }

    public void DiscardElementsOutsideWindow(Rect window, ScrollOrientation orientation)
    {
        Debug.Assert(IsVirtualizingContext());
        Debug.Assert(useLayoutBounds);
        Debug.Assert(_realizedElements.Count == _realizedElementLayoutBounds.Count);

        // The following illustration explains the cutoff indices.
        // We will clear all the realized elements from both ends
        // up to the corresponding cutoff index.
        // '-' means the element is outside the cutoff range.
        // '*' means the element is inside the cutoff range and will be cleared.
        //
        // Window:
        //        |______________________________|
        // Realization range:
        // |*****----------------------------------*********|
        //      |                                  |
        //  frontCutoffIndex                backCutoffIndex
        //
        // Note that we tolerate at most one element outside of the window
        // because the FlowLayoutAlgorithm.Generate routine stops *after*
        // it laid out an element outside the realization window.
        // This is also convenient because it protects the anchor
        // during a BringIntoView operation during which the anchor may
        // not be in the realization window (in fact, the realization window
        // might be empty if the BringIntoView is issued before the first
        // layout pass).

        var realizedRangeSize = GetRealizedElementCount();
        var frontCutoffIndex = -1;
        var backCutoffIndex = realizedRangeSize;

        for (var i =0; 
            i < realizedRangeSize && !Intersects(window, _realizedElementLayoutBounds[i], orientation);
            i++)
        {
            ++frontCutoffIndex;
        }

        for (var i = realizedRangeSize - 1;
            i >= 0 && !Intersects(window, _realizedElementLayoutBounds[i], orientation);
            i--)
        {
            --backCutoffIndex;
        }

        if (backCutoffIndex < realizedRangeSize - 1)
        {
            ClearRealizedRange(backCutoffIndex + 1, realizedRangeSize - backCutoffIndex - 1);
        }

        if (frontCutoffIndex > 0)
        {
            ClearRealizedRange(0, Math.Min(frontCutoffIndex, GetRealizedElementCount()));
        }
    }

    public static bool Intersects(Rect lhs, Rect rhs, ScrollOrientation orientation)
    {
        var lhsStart = orientation == ScrollOrientation.Vertical ? lhs.Y : lhs.X;
        var lhsEnd = orientation == ScrollOrientation.Vertical ? lhs.Y + lhs.Height : lhs.X + lhs.Width;
        var rhsStart = orientation == ScrollOrientation.Vertical ? rhs.Y : rhs.X;
        var rhsEnd = orientation == ScrollOrientation.Vertical ? rhs.Y + rhs.Height : rhs.X + rhs.Width;

        return lhsEnd >= rhsStart && lhsStart <= rhsEnd;
    }

    private void OnItemsAdded(int index, int count)
    {
        // Using the old indices here (before it was updated by the collection change)
        // if the insert data index is between the first and last realized data index, we need
        // to insert items.
        var lastRealizedDataIndex = _firstRealizedDataIndex + GetRealizedElementCount() - 1;
        var newStartingIndex = index;
        if (newStartingIndex >= _firstRealizedDataIndex &&
            newStartingIndex <= lastRealizedDataIndex)
        {
            // Inserted within the realized range
            var insertRangeStartIndex = newStartingIndex - _firstRealizedDataIndex;
            for (var i = 0; i < count; i++)
            {
                // Insert null (sentinel) here instead of an element, that way we dont 
                // end up creating a lot of elements only to be thrown out in the next layout.
                var insertRangeIndex = insertRangeStartIndex + i;
                var dataIndex = newStartingIndex + i;
                // This is to keep the contiguousness of the mapping
                Insert(insertRangeIndex, dataIndex, null);
            }
        }
        else if (index <= _firstRealizedDataIndex)
        {
            // Items were inserted before the realized range.
            // We need to update m_firstRealizedDataIndex;
            _firstRealizedDataIndex += count;
        }
    }

    private void OnItemsRemoved(int index, int count)
    {
        var lastRealizedDataIndex = _firstRealizedDataIndex + _realizedElements.Count - 1;
        var startIndex = Math.Max(_firstRealizedDataIndex, index);
        var endIndex = Math.Min(lastRealizedDataIndex, index + count - 1);
        var removeAffectsFirstRealizedDataIndex = index <= _firstRealizedDataIndex;

        if (endIndex >= startIndex)
        {
            ClearRealizedRange(GetRealizedRangeIndexFromDataIndex(startIndex), endIndex - startIndex + 1);
        }

        if (removeAffectsFirstRealizedDataIndex && _firstRealizedDataIndex != -1)
        {
            _firstRealizedDataIndex -= count;
        }
    }

    [MemberNotNullWhen(true, nameof(_context))]
    private bool ContextNotNull => _context is not null;

    [MemberNotNull(nameof(_context))]
    private bool IsVirtualizingContext()
    {
        Debug.Assert(ContextNotNull);
        var rect = _context.RealizationRect;
        var hasInfiniteSize = double.IsInfinity(rect.Height) || double.IsInfinity(rect.Width);
        return !hasInfiniteSize;
    }

    private readonly List<Control?> _realizedElements = [];
    private readonly List<Rect> _realizedElementLayoutBounds = [];
    private int _firstRealizedDataIndex = -1;
    private VirtualizingLayoutContext? _context;
}
