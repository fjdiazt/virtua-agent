using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace VirtuaAgent.OpenAi;

public sealed class ChatMessageContentSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(ChatMessageContent) || schema is not OpenApiSchema openApiSchema)
        {
            return;
        }

        openApiSchema.Type = null;
        openApiSchema.Properties = new Dictionary<string, IOpenApiSchema>();
        openApiSchema.OneOf =
        [
            new OpenApiSchema
            {
                Type = JsonSchemaType.String
            },
            new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = ContentPartSchema()
            }
        ];
    }

    private static OpenApiSchema ContentPartSchema() => new()
    {
        Type = JsonSchemaType.Object,
        Required = new HashSet<string> { "type" },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "OpenAI content part type, for example text or image_url."
            },
            ["text"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["image_url"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["url"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String }
                },
                AdditionalPropertiesAllowed = false
            }
        },
        AdditionalPropertiesAllowed = false
    };
}
