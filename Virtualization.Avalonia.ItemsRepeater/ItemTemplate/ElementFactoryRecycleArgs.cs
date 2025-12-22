using Avalonia.Controls;

namespace Virtualization.Avalonia;

/// <summary>
/// Represents the optional arguments to use when calling an implementation of the
/// <see cref="IElementFactory"/>'s <see cref="IElementFactory.RecycleElement(ElementFactoryRecycleArgs)"/>
/// </summary>
public class ElementFactoryRecycleArgs
{
    /// <summary>
    /// Gets or sets the <see cref="Control"/> object to recycle when calling
    /// <see cref="IElementFactory.RecycleElement(ElementFactoryRecycleArgs)"/>
    /// </summary>
    public Control Element { get; set; } = null!;

    /// <summary>
    /// Gets or sets a reference to the current parent <see cref="Control"/> of the element being recycled
    /// </summary>
    public Panel Parent { get; set; } = null!;
}
