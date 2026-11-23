using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace WebfrontCore.Core.OpenApi;

/// <summary>
/// Populates OpenAPI tag descriptions from <see cref="TagDescriptionAttribute"/> applied
/// to controllers. The built-in XML-comment source generator does not set tag descriptions,
/// so without this transformer the tag headers in Scalar render blank.
/// Descriptions live on the controller they document — edit the attribute, not this class.
/// </summary>
internal sealed class TagDescriptionsTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var descriptionsByTag = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in context.DescriptionGroups)
        {
            foreach (var apiDescription in group.Items)
            {
                if (apiDescription.ActionDescriptor is not ControllerActionDescriptor action)
                {
                    continue;
                }

                var attr = (TagDescriptionAttribute?)Attribute.GetCustomAttribute(
                    action.ControllerTypeInfo, typeof(TagDescriptionAttribute));

                if (attr is null)
                {
                    continue;
                }

                // Tag name matches what [Tags] / controller-name convention produced in the document.
                var tagName = action.ControllerTypeInfo
                    .GetCustomAttributes(typeof(TagsAttribute), inherit: false)
                    .OfType<TagsAttribute>()
                    .FirstOrDefault()?.Tags.FirstOrDefault()
                    ?? action.ControllerName;

                descriptionsByTag[tagName] = attr.Description;
            }
        }

        if (descriptionsByTag.Count == 0)
        {
            return Task.CompletedTask;
        }

        document.Tags ??= new HashSet<OpenApiTag>();
        var existingByName = document.Tags.ToDictionary(t => t.Name!, t => t, StringComparer.Ordinal);

        foreach (var (name, description) in descriptionsByTag)
        {
            if (existingByName.TryGetValue(name, out var tag))
            {
                if (string.IsNullOrWhiteSpace(tag.Description))
                {
                    tag.Description = description;
                }
            }
            else
            {
                document.Tags.Add(new OpenApiTag { Name = name, Description = description });
            }
        }

        return Task.CompletedTask;
    }
}
