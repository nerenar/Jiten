using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;
        
        schema.Enum.Clear();
        foreach (var value in Enum.GetValues(context.Type))
        {
            var name = Enum.GetName(context.Type, value);
            schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString($"{name} = {(int)value}"));
        }
    }
}