using System.Reflection;
using Microsoft.OpenApi.Models;

namespace NovaWallet.Api.Swagger;

public static class SwaggerSetup
{
    public static IServiceCollection AddNovaWalletSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "NovaWallet Ledger Service",
                Version = "v1",
                Description =
                    "Wallet ledger for FirstBank NovaPay. All monetary amounts are " +
                    "integer kobo (100 kobo = NGN 1). No fractional amounts exist."
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Obtain a token from POST /api/auth/token, then paste it here " +
                    "(without the 'Bearer ' prefix).",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            options.AddSecurityDefinition("Bearer", scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [scheme] = Array.Empty<string>()
            });

            // Surfaces the XML doc comments on controllers and DTOs in the UI.
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}