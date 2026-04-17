namespace WebfrontCore.Core.OpenApi;

/// <summary>
/// Supplies the OpenAPI tag description for a controller. Rendered as the tag header
/// blurb in Scalar / Swagger UI. The tag name itself is derived from the controller
/// name (or from <c>[Tags]</c> if present); this attribute only sets the description.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TagDescriptionAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}
