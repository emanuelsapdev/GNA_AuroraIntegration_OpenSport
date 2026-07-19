using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Application.UseCases;
using GNA.AuroraIntegration.Application.UseCases.Outbound;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Host.Jobs;
using GNA.AuroraIntegration.Host.Startup;
using GNA.AuroraIntegration.Infrastructure.Aurora;
using GNA.AuroraIntegration.Infrastructure.Repositories;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Client;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Repositories;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Services;
using Microsoft.Extensions.Options;
using Quartz;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options => options.ServiceName = "GNA.AuroraIntegration"); // Configurar la aplicación para ejecutarse como un servicio de Windows

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration)); // Configurar Serilog para logging


builder.Services.AddOptions<AuroraApiSettings>()
    .BindConfiguration("AuroraApi")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ServiceLayerSettings>()
    .BindConfiguration("ServiceLayer")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Singleton: AuroraArticleApiClient gestiona internamente su RestClient (misma convención que ServiceLayerClient)
builder.Services.AddSingleton<IAuroraArticleApiClient, AuroraArticleApiClient>();

// Singleton: ServiceLayerClient gestiona internamente su RestClient con CookieContainer
// para mantener la sesión B1SESSION activa durante toda la vida de la aplicación.
builder.Services.AddSingleton<IServiceLayerClient, ServiceLayerClient>();

// Infrastructure: Replicación (store genérico + repos específicos) ----
builder.Services.AddScoped<IReplicationControlStore, ReplicationControlStore>();
builder.Services.AddScoped<ISchemaProvisioningService, ServiceLayerSchemaProvisioningService>();
builder.Services.AddScoped<IArticleLookupRepository, ArticleServiceLayerLookupRepository>();
builder.Services.AddScoped<IArticleReplicationRepository, ArticleReplicationRepository>();

// Agregar repositorios y casos de uso
builder.Services.AddScoped<IEnsureReplicationSchemaUseCase, EnsureReplicationSchemaUseCase>();
builder.Services.AddScoped<IArticleSyncUseCase, ArticleSyncUseCase>();

// ---- Bootstrap de esquema: SIEMPRE antes de Quartz ----
builder.Services.AddHostedService<SchemaBootstrapperHostedService>(); // DESCOMENTAR 

// Configurar Quartz para ejecutar el job de sincronización de artículos
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("ArticlesSyncJob");
    q.AddJob<ArticlesSyncJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(t => t.ForJob(jobKey)
        .WithIdentity("ArticlesSyncJob-trigger")
        .WithCronSchedule(builder.Configuration["Jobs:ArticlesSyncJob:Cron"]!));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

// Configurar el endpoint para recibir eventos de Aurora
app.MapPost("/events/status", () => "Hola Mundo"); // TODO: reemplazar por use case real

// Endpoint de prueba para validar que la API está en línea
app.MapGet("/", () => Results.Ok(new { message = "Endpoint de prueba funcionando" }));

app.Run();