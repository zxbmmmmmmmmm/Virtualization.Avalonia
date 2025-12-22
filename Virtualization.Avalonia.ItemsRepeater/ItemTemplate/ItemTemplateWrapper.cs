using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;

namespace Virtualization.Avalonia;

public class ItemTemplateWrapper(IDataTemplate template) : IElementFactory
{
    public Control GetElement(ElementFactoryGetArgs args)
    {
        if (RecyclePool.TryGetPoolInstance(template) is { } recyclePool
            && recyclePool.TryGetElement("", args.Parent) is { } e)
            return e;

        // no element was found in recycle pool, create a new element
        // if Template returned null, so insert empty element to render nothing
        var element = template.Build(args.Data) ?? new Rectangle();

        // Associate template with element
        element.SetValue(RecyclePool.OriginTemplateProperty, template);

        return element;
    }

    public void RecycleElement(ElementFactoryRecycleArgs args)
    {
        if (RecyclePool.TryGetPoolInstance(template) is not { } recyclePool)
        {
            // No Recycle pool in the template, create one.
            recyclePool = new RecyclePool();
            RecyclePool.SetPoolInstance(template, recyclePool);
        }

        recyclePool.PutElement(args.Element, "", args.Parent);
    }

    bool IDataTemplate.Match(object? data) => template.Match(data);

    Control? ITemplate<object?, Control?>.Build(object? param) => template.Build(param);
}
