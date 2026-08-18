using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Application.UseCases;
using GNA.AuroraIntegration.Application.UseCases.Outbound;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Application.UseCases.Outbound.Decorators;
using GNA.AuroraIntegration.Application.Validation;
using GNA.AuroraIntegration.Host.Health;
using GNA.AuroraIntegration.Host.Jobs;
using GNA.AuroraIntegration.Host.Startup;
using GNA.AuroraIntegration.Infrastructure.Aurora;
using GNA.AuroraIntegration.Infrastructure.Repositories;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Client;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Repositories;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Services;
using Quartz;
using Serilog;
using GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;

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
builder.Services.AddScoped<IPurchaseOrderLookupRepository, PurchaseOrderServiceLayerLookupRepository>();
builder.Services.AddScoped<IPurchaseOrderReplicationRepository, PurchaseOrderReplicationRepository>();
builder.Services.AddScoped<IInventoryTransferRequestLookupRepository, InventoryTransferRequestServiceLayerLookupRepository>();
builder.Services.AddScoped<IInventoryTransferRequestReplicationRepository, InventoryTransferRequestReplicationRepository>();

// Singleton: AuroraPurchaseOrderApiClient gestiona internamente su RestClient (misma convención que AuroraArticleApiClient)
builder.Services.AddSingleton<IAuroraPurchaseOrderApiClient, AuroraPurchaseOrderApiClient>();

// Singleton: AuroraInventoryTransferRequestApiClient gestiona internamente su RestClient (misma convención que AuroraPurchaseOrderApiClient)
builder.Services.AddSingleton<IAuroraInventoryTransferRequestApiClient, AuroraInventoryTransferRequestApiClient>();

// Agregar repositorios y casos de uso
builder.Services.AddScoped<IEnsureReplicationSchemaUseCase, EnsureReplicationSchemaUseCase>();
builder.Services.AddScoped<IArticlePayloadValidator, ArticlePayloadValidator>();
builder.Services.AddScoped<IArticleSyncUseCase, ArticleSyncUseCase>();
builder.Services.Decorate<IArticleSyncUseCase, ArticleSyncUseCaseLoggingDecorator>();

builder.Services.AddScoped<IPurchaseOrderPayloadValidator, PurchaseOrderPayloadValidator>();
builder.Services.AddScoped<IPurchaseOrderSyncUseCase, PurchaseOrderSyncUseCase>();
builder.Services.Decorate<IPurchaseOrderSyncUseCase, PurchaseOrderSyncUseCaseLoggingDecorator>();

builder.Services.AddScoped<IInventoryTransferRequestPayloadValidator, InventoryTransferRequestPayloadValidator>();
builder.Services.AddScoped<IInventoryTransferRequestSyncUseCase, InventoryTransferRequestSyncUseCase>();
builder.Services.Decorate<IInventoryTransferRequestSyncUseCase, InventoryTransferRequestSyncUseCaseLoggingDecorator>();

// ---- Bootstrap de esquema: SIEMPRE antes de Quartz ----
builder.Services.AddHostedService<SchemaBootstrapperHostedService>();  

// Configurar Quartz para ejecutar los jobs de sincronización
builder.Services.AddQuartz(q =>
{
    var articlesJobKey = new JobKey("ArticlesSyncJob");
    q.AddJob<ArticlesSyncJob>(opts => opts.WithIdentity(articlesJobKey));
    q.AddTrigger(t => t.ForJob(articlesJobKey)
        .WithIdentity("ArticlesSyncJob-trigger")
        .WithCronSchedule(builder.Configuration["Jobs:ArticlesSyncJob:Cron"]!));

    var purchaseOrdersJobKey = new JobKey("PurchaseOrdersSyncJob");
    q.AddJob<PurchaseOrdersSyncJob>(opts => opts.WithIdentity(purchaseOrdersJobKey));
    q.AddTrigger(t => t.ForJob(purchaseOrdersJobKey)
        .WithIdentity("PurchaseOrdersSyncJob-trigger")
        .WithCronSchedule(builder.Configuration["Jobs:PurchaseOrdersSyncJob:Cron"]!));

    var inventoryTransferRequestJobKey = new JobKey("InventoryTransferRequestSyncJob");
    q.AddJob<InventoryTransferRequestSyncJob>(opts => opts.WithIdentity(inventoryTransferRequestJobKey));
    q.AddTrigger(t => t.ForJob(inventoryTransferRequestJobKey)
        .WithIdentity("InventoryTransferRequestSyncJob-trigger")
        .WithCronSchedule(builder.Configuration["Jobs:InventoryTransferRequestSyncJob:Cron"]!));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

builder.Services.AddHealthChecks()
    .AddCheck<ServiceLayerHealthCheck>("service-layer");

var app = builder.Build();

// Configurar el endpoint para recibir eventos de Aurora
app.MapPost("/events/status", () => "Hola Mundo"); // TODO: reemplazar por use case real

// Endpoint de prueba para validar que la API está en línea
app.MapGet("/", () => Results.Ok(new { message = "Endpoint de prueba funcionando" }));
app.MapHealthChecks("/health");

app.Run();
