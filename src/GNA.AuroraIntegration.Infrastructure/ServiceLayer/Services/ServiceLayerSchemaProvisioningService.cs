using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Entities.Schema;
using GNA.AuroraIntegration.Domain.Exceptions;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Mapping;
using Microsoft.Extensions.Logging;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Services;

/// <summary>
/// Implementación de ISchemaProvisioningService sobre SAP B1 Service Layer
/// (recursos UserTablesMD / UserFieldsMD). Idempotente.
/// </summary>
public sealed class ServiceLayerSchemaProvisioningService : ISchemaProvisioningService
{
    private readonly IServiceLayerClient _client;
    private readonly ILogger<ServiceLayerSchemaProvisioningService> _logger;

    public ServiceLayerSchemaProvisioningService(
        IServiceLayerClient client,
        ILogger<ServiceLayerSchemaProvisioningService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task EnsureUserTableAsync(UserTableDefinition table, CancellationToken ct = default)
    {
        var existing = await _client.GetAsync<object>($"UserTablesMD('{table.TableName}')", ct);
        if (existing is not null)
        {
            _logger.LogInformation("UDT '{Table}' ya existe. Se omite creación.", table.TableName);
            return;
        }

        try
        {
            await _client.PostAsync<object>("UserTablesMD", new
            {
                TableName        = table.TableName,
                TableDescription = table.TableDescription,
                TableType        = UserTableTypeMapper.ToServiceLayerLiteral(table.TableType)
            }, ct);

            _logger.LogInformation("UDT '{Table}' creada exitosamente.", table.TableName);
        }
        catch (Exception ex)
        {
            throw new SchemaProvisioningException(table.TableName,
                $"No se pudo crear la tabla de usuario '{table.TableName}'.", ex);
        }
    }

    public async Task EnsureUserFieldAsync(
        string tableName, UserFieldDefinition field, CancellationToken ct = default)
    {
        var filter = $"UserFieldsMD?$filter=TableName eq '{tableName}' and Name eq '{field.Name}'";
        var existing = await _client.GetAsync<ServiceLayerCollection<object>>(filter, ct);

        if (existing?.Value is { Count: > 0 })
        {
            _logger.LogInformation(
                "UDF '{Field}' ya existe en '{Table}'. Se omite creación.", field.Name, tableName);
            return;
        }

        try
        {
            await _client.PostAsync<object>("UserFieldsMD", new
            {
                TableName   = tableName,
                Name        = field.Name,
                Description = field.Description,
                Type        = UserFieldTypeMapper.ToServiceLayerLiteral(field.Type),
                SubType     = UserFieldSubTypeMapper.ToServiceLayerLiteral(field.SubType),
                Size        = field.Size
            }, ct);

            _logger.LogInformation("UDF '{Field}' creado en '{Table}'.", field.Name, tableName);
        }
        catch (Exception ex)
        {
            //throw new SchemaProvisioningException($"{tableName}.{field.Name}",
            //    $"No se pudo crear el campo de usuario '{field.Name}' en '{tableName}'.", ex);
            _logger.LogWarning($"{tableName}.{field.Name}",
                $"No se pudo crear el campo de usuario '{field.Name}' en '{tableName}'.");
        }
    }
}

internal sealed class ServiceLayerCollection<T>
{
    public List<T> Value { get; set; } = new();
}
