using Microsoft.OpenApi.Models;

namespace PartnerIntegration.API.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithApiKey(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Partner Integration BFF",
                Version = "v1",
                Description = "Backend-for-Frontend microservice for partner transaction processing."
            });

            // API Key security definition
            c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-Api-Key",
                Description = "API Key for authentication"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "ApiKey"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
