# GNA.AuroraIntegration — Instrucciones para GitHub Copilot

Estas instrucciones se aplican a **todo** el código generado o modificado en este repositorio.
Léelas completas antes de proponer cualquier cambio.

---

## 1. Arquitectura — Clean Architecture estricta

### Capas y dependencias permitidas

```
Domain  ←  Application  ←  Infrastructure  ←  Host
```

| Capa | Proyecto | Puede referenciar |
|---|---|---|
| **Domain** | `GNA.AuroraIntegration.Domain` | Solo BCL (.NET). Sin dependencias de NuGet externas. |
| **Application** | `GNA.AuroraIntegration.Application` | Domain únicamente. |
| **Infrastructure** | `GNA.AuroraIntegration.Infrastructure` | Domain + Application. **Nunca** al revés. |
| **Host** | `GNA.AuroraIntegration.Host` | Todos. Solo bootstrapping (DI, configuración, jobs). |
| **Tests** | `GNA.AuroraIntegration.Tests` | Application + Domain. Mock de interfaces, nunca de clases concretas. |

### Regla de dependencias — Dependency Rule
- El código de una capa **nunca importa** un namespace de una capa exterior.
- Si Domain necesita algo de Infrastructure, es señal de que falta una interfaz en Domain.

### Estructura de carpetas por capa

```
Domain/
  Constants/        ← nombres lógicos (sin prefijos tecnológicos)
  Entities/         ← entidades y value objects puros
  Enums/            ← enums del dominio
  Exceptions/       ← excepciones de dominio (heredan de AuroraIntegrationException)
  Interfaces/       ← contratos de repositorios y servicios de dominio

Application/
  DTOs/             ← objetos de transferencia (entrada/salida de use cases)
  Interfaces/       ← contratos de clientes externos (IServiceLayerClient, IAuroraApiClient)
  UseCases/         ← un archivo por caso de uso + su interfaz en el mismo archivo
  UseCases/Outbound/← use cases de sincronización saliente hacia Aurora

Infrastructure/
  Aurora/           ← cliente HTTP Aurora y sus settings
  Repositories/     ← implementaciones de IRepository que combinan stores
  ServiceLayer/
	Client/         ← ServiceLayerClient + ServiceLayerSettings
	Constants/      ← constantes SAP B1 (endpoints U_*, campos U_*, literales)
	Mapping/        ← traductores enum dominio → literal Service Layer
	Repositories/   ← repositorios que leen datos de SAP B1 vía SL
	Services/       ← implementaciones de servicios de dominio (ISchemaProvisioningService)

Host/
  Jobs/             ← Quartz jobs (delegan todo al use case, sin lógica propia)
  Startup/          ← hosted services de bootstrap
```

---

## 2. Principios SOLID aplicados a este proyecto

### S — Single Responsibility
- Cada clase tiene **una sola razón para cambiar**.
- Un use case = una operación de negocio. No mezclar sincronización con provisioning.
- Los Quartz jobs solo llaman al use case; nunca tienen lógica de negocio propia.
- Los mappers (`UserFieldTypeMapper`, etc.) solo traducen; no validan ni loguean.

### O — Open/Closed
- Para soportar una nueva entidad replicable: agregar un valor a `ReplicableEntityType`
  y una constante en `ReplicationSchemaConstants`. **Sin tocar** `ReplicationControlStore`.
- Para un nuevo destino de replicación: implementar `IAuroraApiClient` con un nuevo adaptador.

### L — Liskov Substitution
- Las implementaciones de interfaces de dominio (`ISchemaProvisioningService`, `IReplicationControlStore`)
  deben ser intercambiables sin alterar el comportamiento observable del use case.
- No lanzar excepciones no declaradas; ampliar la jerarquía `AuroraIntegrationException` cuando sea necesario.

### I — Interface Segregation
- No agregar métodos a una interfaz existente si solo una implementación los necesita.
- Ejemplo correcto: `IArticleReplicationRepository` expone solo operaciones de `Article`;
  no expone los métodos genéricos de `IReplicationControlStore`.

### D — Dependency Inversion
- Los use cases dependen de interfaces (`IArticleReplicationRepository`, `IAuroraApiClient`), nunca de clases concretas.
- Las clases concretas viven en Infrastructure; se registran en Host/Program.cs.
- Usar siempre constructor injection. Prohibido `ServiceLocator` o `IServiceProvider` fuera de Program.cs.

---

## 3. Constantes — cuándo y dónde

### Regla general
> Si un string o número aparece más de una vez **o** tiene significado semántico, debe ser una constante.

### Dónde definirlas según su naturaleza

| Tipo de constante | Capa | Archivo de referencia |
|---|---|---|
| Nombres lógicos de tablas UDT (`GNA_REP_QUEUE`) | **Domain** | `Domain/Constants/ReplicationSchemaConstants.cs` |
| Nombres físicos con `@` (`@GNA_REP_QUEUE`) | **Domain** | `ReplicationSchemaConstants.*.DbName` |
| Endpoints Service Layer (`U_GNA_REP_QUEUE`) | **Infrastructure** | `ServiceLayer/Constants/SapB1ReplicationConstants.cs` |
| Campos con prefijo `U_` | **Infrastructure** | `SapB1ReplicationConstants.*.Queue.*` |
| Literales de estado/operación SAP B1 (`PENDING`, `I`, `U`) | **Infrastructure** | `SapB1ReplicationConstants.*.StatusValues.*` |
| Rutas de endpoints Aurora (`/aurora-erp/articles`) | **Infrastructure** | Constante `internal` en el cliente Aurora |

### Checklist antes de escribir un string literal
- [ ] ¿Aparece en más de un lugar? → extraer a constante.
- [ ] ¿Es un nombre de tabla, campo o endpoint? → constante obligatoria.
- [ ] ¿Es un valor de configuración (URL, cron)? → `appsettings.json` + clase `*Settings`.
- [ ] ¿Es un mensaje de log? → puede quedar inline.
- [ ] ¿Es un mensaje de excepción? → puede quedar inline, pero solo en la excepción del dominio.

---

## 4. Casos de uso — contrato obligatorio

### Estructura de un use case nuevo

```csharp
// Application/UseCases/[Subfolder]/MiNuevoUseCase.cs
namespace GNA.AuroraIntegration.Application.UseCases.[Subfolder];

// 1. Interfaz y clase en el mismo archivo
public interface IMiNuevoUseCase
{
	Task<ResultType> ExecuteAsync(InputType input, CancellationToken ct = default);
}

// 2. Implementación sealed
public sealed class MiNuevoUseCase : IMiNuevoUseCase
{
	// 3. Solo interfaces en el constructor
	public MiNuevoUseCase(IDependencia1 dep1, IDependencia2 dep2) { ... }

	public async Task<ResultType> ExecuteAsync(InputType input, CancellationToken ct = default)
	{
		// lógica...
	}
}
```

### Registro en DI (Program.cs)
```csharp
builder.Services.AddScoped<IMiNuevoUseCase, MiNuevoUseCase>();
```

### Test obligatorio — uno por use case
Por cada use case **debe existir** un archivo de test con la forma:
`tests/GNA.AuroraIntegration.Tests/[NombreUseCase]Tests.cs`

Mínimo de tests exigidos por use case:

| # | Nombre del test | Qué verifica |
|---|---|---|
| 1 | `ExecuteAsync_ShouldCallDependencies_WithExpectedParameters` | Que las dependencias reciben los parámetros correctos |
| 2 | `ExecuteAsync_ShouldCompleteSuccessfully` | Happy path sin excepciones |
| 3 | `ExecuteAsync_ShouldHandle[DomainException]` | Manejo de error de negocio esperado |
| 4 | `ExecuteAsync_ShouldPassCancellationToken` | Que el CT se propaga a todas las dependencias |
| 5 | `ExecuteAsync_WhenServiceFails_ShouldPropagate` | Que las excepciones de infraestructura no se swallowean |

### Ejemplo de estructura de test
```csharp
public class MiNuevoUseCaseTests
{
	private readonly Mock<IDependencia1> _mockDep1 = new();
	private readonly MiNuevoUseCase _useCase;

	public MiNuevoUseCaseTests()
		=> _useCase = new MiNuevoUseCase(_mockDep1.Object);

	[Fact(DisplayName = "✓ Descripción clara del test")]
	public async Task ExecuteAsync_ShouldXxx_WhenYyy()
	{
		// Arrange
		// Act
		// Assert — usar constantes del dominio, nunca strings literales
	}
}
```

> ⚠️ Los tests **nunca** usan strings literales de nombres de tablas, campos o estados.
> Siempre referencian `ReplicationSchemaConstants.*` o `SapB1ReplicationConstants.*`.

---

## 5. Excepciones — jerarquía del dominio

Toda excepción de negocio hereda de `AuroraIntegrationException`:

```
AuroraIntegrationException  (base, Domain)
├── SchemaProvisioningException
├── ArticleNotFoundException
└── ArticleReplicationException
	└── [NuevaEntidad]ReplicationException  ← agregar aquí si se suma una entidad
```

- Lanzar excepciones de dominio **desde Infrastructure**, atraparlas **en Application**.
- Nunca exponer `HttpRequestException` o `SqlException` a capas superiores sin envolver.

---

## 6. Logging — estándar

- Usar siempre `ILogger<T>` de `Microsoft.Extensions.Logging`. **No** inyectar `Serilog.ILogger` directamente en clases de Application o Domain.
- Niveles:
  - `LogInformation` → operación completada exitosamente.
  - `LogWarning` → situación inesperada pero recuperable (entidad no encontrada en cola).
  - `LogError` → fallo que impide completar la operación.
  - `LogDebug` → flujo de idempotencia, deduplicación, skips.
- Los mensajes estructurados deben usar placeholders nombrados: `"Replicando '{Sku}' [{Type}]"`.

---

## 7. Entidad nueva replicable — checklist

Cuando se agrega soporte para una entidad nueva (ej: `PurchaseOrder`):

- [ ] Agregar valor en `Domain/Enums/ReplicableEntityType.cs`
- [ ] Agregar interfaz `I[Entidad]ReplicationRepository` en `Domain/Interfaces/`
- [ ] Agregar implementación en `Infrastructure/Repositories/`
- [ ] Agregar use case en `Application/UseCases/Outbound/`
- [ ] Agregar DTO en `Application/DTOs/`
- [ ] Agregar excepción en `Domain/Exceptions/` si tiene errores propios
- [ ] Registrar en `Host/Program.cs`
- [ ] Crear `tests/.../[Entidad]SyncUseCaseTests.cs` con los 5 tests mínimos

---

## 8. SAP B1 Service Layer — convenciones

- Endpoint de UDT NoObject: `U_{TableName}` (sin `@`).
- Clave primaria de UDT: campo `Code` (string, máx. 8 chars alfanuméricos). Generar con `Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()`.
- Campos de usuario: prefijo `U_` obligatorio en Service Layer.
- Actualizar siempre con `PATCH`; nunca `PUT`.
- Serialización de fechas para UDTs: `"yyyy-MM-dd"`.
- Los filtros OData deben escapar apóstrofos: `value.Replace("'", "''")`.

---

## 9. Mejoras propuestas (backlog técnico)

Las siguientes mejoras están identificadas y deben considerarse antes de crecer la superficie de código:

### 9.1 Estandarizar ILogger en ArticleSyncUseCase
`ArticleSyncUseCase` inyecta `Serilog.ILogger` en lugar de `Microsoft.Extensions.Logging.ILogger<T>`.
Corregir para ser consistente con el resto del proyecto y facilitar testing.

### 9.2 Result pattern en lugar de excepciones para flujo de negocio
Los casos donde "no encontrado" es una respuesta válida (no un error) deberían retornar
`Result<T>` o usar el patrón `Try*` en lugar de lanzar excepciones.
Librería sugerida: `FluentResults` o un `Result<T>` propio en Domain.

### 9.3 Validación de DTOs de entrada en Application
Agregar validación explícita de los inputs de cada use case usando `FluentValidation`
o `DataAnnotations`. Los errores de validación deben lanzar una excepción de dominio,
no llegar a Infrastructure.

### 9.4 Validación de Settings al arrancar
Usar `IValidateOptions<T>` (o `.ValidateDataAnnotations().ValidateOnStart()`) para
`ServiceLayerSettings` y `AuroraApiSettings`. Así el servicio falla rápido con mensaje claro
si falta configuración en lugar de fallar en el primer request.

```csharp
builder.Services.AddOptions<ServiceLayerSettings>()
	.BindConfiguration("ServiceLayer")
	.ValidateDataAnnotations()
	.ValidateOnStart();
```

### 9.5 Resiliencia HTTP con Polly
Agregar políticas de reintento y circuit-breaker para `ServiceLayerClient` y `AuroraApiClient`.
Configurar en Program.cs vía `.AddResilienceHandler(...)` (Microsoft.Extensions.Http.Resilience, .NET 8+).

### 9.6 Health Checks
Exponer un endpoint `/health` que verifique conectividad contra Service Layer.
Usar `IHealthCheck` de `Microsoft.Extensions.Diagnostics.HealthChecks`.

### 9.7 Paginación en GetPendingKeysAsync
El parámetro `batchSize` existe pero no hay control de cursor/offset.
Agregar soporte para procesar páginas sucesivas sin recargar las mismas claves.

### 9.8 Separar interfaz del use case a su propio archivo
Actualmente la interfaz `IXxxUseCase` y su implementación conviven en el mismo archivo.
Moverlas a archivos separados mejora la navegación y el diff en PR.
Convenio:
- `UseCases/Outbound/IArticleSyncUseCase.cs`
- `UseCases/Outbound/ArticleSyncUseCase.cs`

### 9.9 Pipeline de comportamientos transversales (MediatR o decoradores)
Centralizar logging, validación y manejo de excepciones en un pipeline en lugar de
repetirlos en cada use case. Opciones:
- **MediatR** con `IPipelineBehavior<TRequest, TResponse>` (CQRS explícito).
- **Decoradores manuales** registrados en DI con `Scrutor`.

---

## 10. Convenciones de código

- `sealed` en todas las clases que no están diseñadas para herencia.
- `init` en propiedades de DTOs y entidades inmutables.
- `required` en propiedades obligatorias de entidades y DTOs.
- Colecciones vacías: `Array.Empty<T>()` o `[]` (collection expression .NET 8+).
- Namespaces deben coincidir con la estructura de carpetas.
- Un tipo público por archivo (excepto la interfaz del use case y su implementación,
  que pueden convivir hasta que se implemente la mejora 9.8).
- No usar `var` cuando el tipo no es evidente en la misma línea.
- Métodos privados de infraestructura que no lanzan excepciones: sufijo `Async` obligatorio.
