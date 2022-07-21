using Newtonsoft.Json;

namespace Juice.CompnentModel
{
    /// <summary>
    /// If you wan to ignore property in swagger doc but need property for JSON serialize/deserialize
    /// <para>so you could not use <see cref="JsonIgnoreAttribute" /> but use <see cref="ApiIgnoreAttribute"/> instead.</para>
    /// <para>An implementation of <see cref="Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter"/> will exclude all properties that have <see cref="ApiIgnoreAttribute"/> </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ApiIgnoreAttribute : Attribute
    {
    }
}
