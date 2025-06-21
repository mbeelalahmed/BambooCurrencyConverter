using CurrencyConverter.API.Helpers;
using CurrencyConverter.API.Middleware;
using CurrencyConverter.API.Models;
using CurrencyConverter.Application.Interfaces;
using CurrencyConverter.Application.Services;
using CurrencyConverter.Infrastructure.Logging;
using CurrencyConverter.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using System.Text;
using System.Text.Json;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();

        builder.Services.AddControllers();

        builder.Services.AddMemoryCache();
        /*
        // To scale out
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("Redis");
            options.InstanceName = "CurrencyConverter:";
        });

        */
        builder.Services.Configure<FrankfurterOptions>(
            builder.Configuration.GetSection("ExchangeRateProviders:Frankfurter"));

        builder.Services.AddHttpClient<IExchangeRateService, FrankfurterService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<FrankfurterOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });


        builder.Services.AddScoped<IExchangeRateService, FrankfurterService>();
        builder.Services.AddScoped<ExchangeRateServiceProviderFactory>();
        builder.Services.AddScoped<ILoggingService, SerilogLoggingService>();

        // In real scenario JWT Key should be saved in confidential key data store (e.g. GitHub Actions-Secrets). For now storing it in appsettings for simplicity.
        var jwtKey = builder.Configuration["Jwt:Key"];
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("GetLatest", policy => policy.RequireRole("admin", "operator"));
            options.AddPolicy("Convert", policy => policy.RequireRole("admin"));
            options.AddPolicy("GetHistorical", policy => policy.RequireRole("admin"));
        });

        // It conceptual implementation 
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("CurrencyConverter.API"))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("CurrencyConverter")
                    .AddJaegerExporter(o =>
                    {
                        o.AgentHost = builder.Configuration["Jaeger:Host"] ?? "localhost";
                        o.AgentPort = int.Parse(builder.Configuration["Jaeger:Port"] ?? "6831");
                    });
            });

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithCorrelationId()
            .Enrich.WithEnvironmentName()
            //  .Enrich.WithOpenTelemetryTraceId()  // Include trace ID in logs
            .WriteTo.Console()
            .WriteTo.Seq("http://localhost:5341")
            .CreateLogger();

        builder.Host.UseSerilog();

        /* To enable distributed Tracing
        builder.Services.AddOpenTelemetryTracing(b =>
        {
            b
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("CurrencyConverterAPI"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .AddZipkinExporter(o =>
            {
                o.Endpoint = new Uri("http://localhost:9411/api/v2/spans");
            });
        });
        */

        builder.Services.AddRateLimiter(_ => _.AddSlidingWindowLimiter("fixed", options =>
        {
            options.PermitLimit = 50;
            options.Window = TimeSpan.FromMinutes(1);
        }));

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Enter Bearer token",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
            });
        });


        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        builder.Services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();

        app.UseMiddleware<RequestLoggingMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseRateLimiter();

        app.MapControllers();

        app.Run();
    }
}