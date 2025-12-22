using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Virtualization.Avalonia;

/// <summary>
/// Supports the creation and recycling of <see cref="Control"/> objects
/// </summary>
public interface IElementFactory : IDataTemplate
{
    /// <summary>
    /// Gets a <see cref="Control"/> object
    /// </summary>
    Control GetElement(ElementFactoryGetArgs args);

    /// <summary>
    /// Recycles a <see cref="Control"/> that was previously retrieved using <see cref="GetElement(ElementFactoryGetArgs)"/>
    /// </summary>
    void RecycleElement(ElementFactoryRecycleArgs args);
}
