# GNA AuroraIntegration — Estado del Proyecto

> **Propósito de este archivo:** Servir como punto de partida para sesiones con IA.
> Leerlo completo al inicio de cada sesión evita explorar el código desde cero.
> **Actualizar este archivo cada vez que se implemente, cambie o elimine algo.**

---

## Descripción general

Servicio de integración entre **SAP B1** (vía Service Layer REST) y **Aurora WMS** (API REST).
Es un Worker Service de .NET 10 que corre como **Windows Service**, ejecuta sincronización de artículos
de forma programada (Quartz.NET) y expone endpoints mínimos de salud e ingesta de eventos.

**Flujo central (Outbound):**
1. SAP B1 enola un artículo en `@GNA_AUR_REP_QUEUE` (vía Stored Procedure disparado desde `SBO_SP_TransactionNotification`).
2. El job de Quartz (`ArticlesSyncJob`) llama a `ArticleSyncUseCase`.
3. El use case lee los SKUs pendientes de la cola, obtiene el `Article` completo desde Service Layer, y lo crea o actualiza en Aurora WMS.
4. Marca el artículo como `REPLICATED` o `FAILED` en la cola con historial en `@GNA_AUR_REP_ATTEMPT`.

---

## Stack técnico

| Tecnología | Uso |
|---|---|
| .NET 10 | Framework base |
| RestSharp 114 | Cliente HTTP (Service Layer y Aurora API) |
| Polly | Resiliencia HTTP (retry + circuit breaker) |
| Quartz.NET | Scheduler de jobs |
| Serilog | Logging (consola + archivos rotativos diarios en `logs/`) |
| Scrutor | Decoradores de DI (`.Decorate<>()`) |
| xUnit + Moq | Tests unitarios |
| `Microsoft.Extensions.Hosting.WindowsServices` | Ejecución como Windows Service |

---

## Arquitectura — Clean Architecture

```
Domain  ←  Application  ←  Infrastructure  ←  Host
```

| Proyecto | Responsabilidad | Dependencias permitidas |
|---|---|---|
| `GNA.AuroraIntegration.Domain` | Entidades, enums, interfaces, excepciones, constantes de esquema (sin prefijos SAP) | Solo BCL |
| `GNA.AuroraIntegration.Application` | Use cases, DTOs, validación, interfaces de clientes externos | Domain |
| `GNA.AuroraIntegration.Infrastructure` | ServiceLayerClient, AuroraArticleApiClient, repos, mappers, constantes SAP | Domain + Application |
| `GNA.AuroraIntegration.Host` | Program.cs (DI), Quartz jobs, health checks, hosted services de bootstrap | Todos |
| `GNA.AuroraIntegration.Tests` | Tests unitarios (xUnit + Moq) | Application + Domain |

---

## Estructura de archivos (solo código propio, sin `obj/`)

```
src/
├── GNA.AuroraIntegration.Domain/
│   ├── Constants/
│   │   └── ReplicationSchemaConstants.cs        ← nombres lógicos de UDTs/campos (sin '@', sin 'U_')
│   ├── Entities/
│   │   ├── Article.cs                           ← entidad principal replicable
│   │   ├── LogisticsCategory.cs
│   │   ├── ProductBrand.cs
│   │   └── Schema/
│   │       ├── UserFieldDefinition.cs
│   │       ├── UserObjectDefinition.cs
│   │       └── UserTableDefinition.cs
│   ├── Enums/
│   │   ├── ReplicableEntityType.cs              ← discriminador: Article (más vendrán)
│   │   ├── ReplicationOperationType.cs          ← Insert | Update
│   │   ├── ReplicationStatus.cs
│   │   └── Schema/
│   │       ├── UserFieldSubType.cs
│   │       ├── UserFieldType.cs
│   │       ├── UserObjectType.cs
│   │       └── UserTableType.cs
│   ├── Exceptions/
│   │   ├── AuroraIntegrationException.cs        ← base de todas las excepciones
│   │   ├── ArticleAuroraApiException.cs
│   │   ├── ArticleNotFoundException.cs
│   │   ├── ArticleRepositoryException.cs
│   │   ├── ReplicationControlStoreException.cs
│   │   ├── SchemaProvisioningException.cs
│   │   └── UseCaseValidationException.cs
│   └── Interfaces/
│       ├── IArticleLookupRepository.cs          ← leer Items de SAP B1
│       ├── IArticleReplicationRepository.cs     ← cola específica de Article
│       ├── IReplicationControlStore.cs          ← store genérico de cola/intentos
│       └── ISchemaProvisioningService.cs        ← provisionar UDTs/UDFs/UDOs

├── GNA.AuroraIntegration.Application/
│   ├── DTOs/Aurora/
│   │   ├── AuroraArticleDto.cs                  ← respuesta GET de Aurora
│   │   ├── CreateAuroraArticleDto.cs            ← payload POST a Aurora (con DataAnnotations)
│   │   ├── GroupOfArticleDto.cs
│   │   └── UpdateAuroraArticleDto.cs            ← payload PATCH a Aurora
│   ├── Interfaces/
│   │   ├── IAuroraArticleApiClient.cs           ← contrato del cliente Aurora
│   │   └── IServiceLayerClient.cs               ← contrato del cliente Service Layer
│   ├── UseCases/
│   │   ├── EnsureReplicationSchemaUseCase.cs    ← provisiona UDTs/UDFs/UDOs al arrancar
│   │   ├── IEnsureReplicationSchemaUseCase.cs
│   │   └── Outbound/
│   │       ├── ArticleSyncUseCase.cs            ← IMPLEMENTADO Y FUNCIONAL
│   │       ├── LogisticsCategorySyncUseCase.cs  ← STUB (devuelve dummy values)
│   │       ├── ProductBrandsSyncUseCase.cs      ← STUB (devuelve dummy values)
│   │       ├── Decorators/
│   │       │   ├── ArticleSyncUseCaseLoggingDecorator.cs
│   │       │   ├── LogisticsCategorySyncUseCaseLoggingDecorator.cs
│   │       │   └── ProductBrandsSyncUseCaseDecorator.cs
│   │       └── Interfaces/
│   │           ├── IArticleSyncUseCase.cs
│   │           ├── ILogisticsCategorySyncUseCase.cs
│   │           └── IProductBrandsSyncUseCase.cs
│   └── Validation/
│       ├── ArticlePayloadValidator.cs           ← valida DTOs de Aurora con DataAnnotations
│       └── IArticlePayloadValidator.cs

├── GNA.AuroraIntegration.Infrastructure/
│   ├── Aurora/
│   │   ├── AuroraApiSettings.cs                 ← BaseUrl, ApiKey
│   │   └── AuroraArticleApiClient.cs            ← cliente HTTP Aurora (retry + circuit breaker)
│   ├── Repositories/
│   │   ├── ArticleReplicationRepository.cs      ← adapta IArticleReplicationRepository sobre el store genérico
│   │   └── ReplicationControlStore.cs           ← IReplicationControlStore sobre UDTs SAP B1
│   ├── Requireds/
│   │   ├── SP_GNAEA_ENQUEUE_ARTICLE_REPLICATION.txt  ← SP HANA que encola artículos
│   │   └── SBO_SP_TransactionNotification.txt        ← hook SAP B1 que llama al SP anterior
│   └── ServiceLayer/
│       ├── Client/
│       │   ├── ServiceLayerClient.cs            ← cliente HTTP SL (session cookie B1SESSION, retry + CB)
│       │   └── ServiceLayerSettings.cs          ← BaseUrl, CompanyDB, UserName, Password
│       ├── Constants/
│       │   ├── SapB1ItemsConstants.cs           ← campos de Items (ItemCode, BarCode, U_GNA_AUR_*)
│       │   └── SapB1ReplicationConstants.cs     ← endpoints U_*, campos U_*, estados PENDING/REPLICATED/FAILED
│       ├── Mapping/
│       │   ├── SapYesNoMapper.cs
│       │   ├── UserFieldSubTypeMapper.cs
│       │   ├── UserFieldTypeMapper.cs
│       │   ├── UserObjectTypeMapper.cs
│       │   └── UserTableTypeMapper.cs
│       ├── Repositories/
│       │   └── ArticleServiceLayerLookupRepository.cs  ← lee Items de SAP B1, mapea a Article
│       └── Services/
│           └── ServiceLayerSchemaProvisioningService.cs ← ISchemaProvisioningService sobre Service Layer

└── GNA.AuroraIntegration.Host/
    ├── Health/
    │   └── ServiceLayerHealthCheck.cs           ← IHealthCheck para /health
    ├── Jobs/
    │   └── ArticlesSyncJob.cs                   ← [DisallowConcurrentExecution] Quartz job
    ├── Startup/
    │   └── SchemaBootstrapperHostedService.cs   ← corre EnsureReplicationSchemaUseCase al arrancar
    ├── Program.cs                               ← DI, Quartz, Serilog, Windows Service
    ├── appsettings.json                         ← estructura de config (vacía, se llena en Development)
    └── appsettings.Development.json             ← valores reales para desarrollo local

tests/
└── GNA.AuroraIntegration.Tests/
    ├── ArticleSyncUseCaseTests.cs               ← 5 tests (todos implementados)
    └── EnsureReplicationSchemaUseCaseTests.cs   ← tests del schema bootstrapper
```

---

## Esquema en SAP B1 (UDTs / UDFs / UDOs)

Provisionado automáticamente al arrancar por `EnsureReplicationSchemaUseCase` → `SchemaBootstrapperHostedService`.

### Tablas UDT

| Nombre lógico | Nombre físico SAP | Endpoint SL | Descripción |
|---|---|---|---|
| `GNA_AUR_REP_QUEUE` | `@GNA_AUR_REP_QUEUE` | `U_GNA_AUR_REP_QUEUE` | Cola viva de replicación pendiente |
| `GNA_AUR_REP_ATTEMPT` | `@GNA_AUR_REP_ATTEMPT` | `U_GNA_AUR_REP_ATTEMPT` | Histórico de intentos (éxito y fallo) |
| `GNA_AUR_CATLOG` | `@GNA_AUR_CATLOG` | — | Categorías Logísticas (MasterData) |
| `GNA_AUR_MARCAS` | `@GNA_AUR_MARCAS` | — | Marcas de Productos (MasterData) |

### Campos UDF agregados a OITM (Items)

| Campo lógico | Nombre físico | Tipo | Descripción |
|---|---|---|---|
| `GNA_AUR_CatLog` | `U_GNA_AUR_CatLog` | Alpha(30) | Categoría Logística (linked a GNA_AUR_CATLOG) |
| `GNA_AUR_Marca` | `U_GNA_AUR_Marca` | Alpha(30) | Marca (linked a GNA_AUR_MARCAS) |
| `GNA_AUR_IsBulky` | `U_GNA_AUR_IsBulky` | Alpha(1) Y/N | Es Voluminoso (default "N") |
| `GNA_AUR_IsCaged` | `U_GNA_AUR_IsCaged` | Alpha(1) Y/N | Es Enjaulado (default "N") |
| `GNA_AUR_Banner` | `U_GNA_AUR_Banner` | Alpha(150) | Banner del artículo |

> **Nota:** Los campos que lee `ArticleServiceLayerLookupRepository` del recurso `Items` son:
> `U_GNA_AUR_BannerID`, `U_GNA_AUR_BrandID`, `U_GNA_AUR_CategoryName`, `U_GNA_AUR_IsBulky`, `U_GNA_AUR_IsCaged`
> (definidos en `SapB1ItemsConstants`). Verificar coherencia con los UDFs provisionados si se añaden campos.

### UDOs creados

| Code | Tabla | Descripción |
|---|---|---|
| `CatLog` | `GNA_AUR_CATLOG` | Categorías Logísticas (menú FatherMenuID 11520, pos 14) |
| `Marcas` | `GNA_AUR_MARCAS` | Marcas de Productos (menú FatherMenuID 11520, pos 15) |

---

## Campos de la cola `@GNA_AUR_REP_QUEUE`

| Campo SAP | Valores posibles |
|---|---|
| `U_GNA_AUR_EntityType` | `Article` (más en el futuro) |
| `U_GNA_AUR_EntityKey` | SKU del artículo (u otra clave natural) |
| `U_GNA_AUR_Operation` | `I` (Insert) / `U` (Update) |
| `U_GNA_AUR_Status` | `PENDING` / `REPLICATED` / `FAILED` |
| `U_GNA_AUR_RetryCount` | Número de reintentos (máx. 4 para Article) |

---

## Use Cases — estado actual

| Use Case | Interfaz | Estado | Notas |
|---|---|---|---|
| `EnsureReplicationSchemaUseCase` | `IEnsureReplicationSchemaUseCase` | ✅ Implementado | Provisiona toda la UDT/UDF/UDO al arrancar |
| `ArticleSyncUseCase` | `IArticleSyncUseCase` | ✅ Implementado | Flujo completo create/update con validación |
| `LogisticsCategorySyncUseCase` | `ILogisticsCategorySyncUseCase` | ⚠️ STUB | Solo devuelve valores dummy. Falta implementación real |
| `ProductBrandsSyncUseCase` | `IProductBrandsSyncUseCase` | ⚠️ STUB | Solo devuelve valores dummy. Falta implementación real |

---

## Entidad `Article` — campos utilizados vs. comentados

**Activos:**
- `Sku`, `Name`, `PrimaryEan`, `AdditionalEans[]`
- `CategoryName`, `BrandID`, `BannerID`
- `IsBulky`, `IsCaged`

**Comentados (disponibles en DTO pero no en uso):**
- `WeightInGr`, `HeightInCm`, `WidthInCm`, `LengthInCm`
- `Colour`, `Size`, `HasProductionBatch`, `HasDueDate`, `HasSerialNumber`, `IsConsumable`
- `BrandName` (se usa `BrandID` pero `BrandExternalId` está comentado en el mapping de ArticleSyncUseCase)

---

## Resiliencia HTTP

Ambos clientes (`ServiceLayerClient` y `AuroraArticleApiClient`) tienen:
- **Retry:** 3 intentos con backoff exponencial (2^n segundos) para `HttpRequestException`
- **Circuit Breaker:** 5 fallos → abierto 30 segundos
- Errores transitorios: HTTP 408, 5xx o status 0 (sin respuesta)

`ServiceLayerClient` además maneja:
- Login automático con cookie `B1SESSION` (CookieContainer como Singleton)
- Re-login automático en HTTP 401 (una sola vez por request)
- `SemaphoreSlim` para evitar logins concurrentes
- Certificados auto-firmados aceptados (on-premise SAP)

---

## Configuración (`appsettings.json`)

```json
{
  "AuroraApi": {
    "BaseUrl": "...",
    "ApiKey": "..."
  },
  "ServiceLayer": {
    "BaseUrl": "...",
    "CompanyDB": "...",
    "UserName": "...",
    "Password": "..."
  },
  "Jobs": {
    "ArticlesSyncJob": { "Cron": "0 * * * * ?" }
  },
  "Serilog": { ... }
}
```

Ambas `AuroraApiSettings` y `ServiceLayerSettings` tienen `.ValidateDataAnnotations().ValidateOnStart()` — el servicio falla al arrancar si falta configuración.

---

## Stored Procedures SAP B1 (HANA)

Ubicación en repo: `src/GNA.AuroraIntegration.Infrastructure/Requireds/`

### `SP_GNAEA_ENQUEUE_ARTICLE_REPLICATION`
- Se llama desde `SBO_SP_TransactionNotification`
- Detecta Add (`A`) / Update (`U`) en `object_type = '4'` (Items)
- Evita duplicados: solo encola si no hay un `PENDING` para el mismo SKU
- Inserta directamente en `@GNA_AUR_REP_QUEUE`

### `SBO_SP_TransactionNotification`
- Hook estándar de SAP B1
- Únicamente llama al SP anterior y propaga error/mensaje

---

## Endpoints HTTP expuestos

| Método | Ruta | Estado | Descripción |
|---|---|---|---|
| `GET` | `/` | ✅ | Prueba de vida |
| `GET` | `/health` | ✅ | Health check (Service Layer) |
| `POST` | `/events/status` | ⚠️ TODO | Recibir eventos de Aurora (retorna "Hola Mundo") |

---

## Tests — cobertura actual

| Archivo | Tests | Estado |
|---|---|---|
| `ArticleSyncUseCaseTests.cs` | 5 tests | ✅ Completos (happy path, create/update, error handling, CT propagation, propagación de excepción) |
| `EnsureReplicationSchemaUseCaseTests.cs` | En el archivo | ✅ Existe |

---

## Pendientes / TODOs conocidos

### Críticos (bloquean funcionalidad completa)
- [ ] Implementar `LogisticsCategorySyncUseCase` real (lee de SAP B1 y sincroniza con Aurora)
- [ ] Implementar `ProductBrandsSyncUseCase` real
- [ ] Implementar endpoint `POST /events/status` con use case real (inbound desde Aurora)

### Menores / mejoras técnicas (de `copilot-instructions.md` sección 9)
- [ ] `BrandExternalId` está comentado en `ArticleSyncUseCase` mapping (líneas 104 y 117). Definir si Aurora lo requiere
- [ ] Verificar coherencia entre campos que lee `ArticleServiceLayerLookupRepository` (`U_GNA_AUR_BannerID`, etc.) y los UDFs provisionados por `EnsureReplicationSchemaUseCase` (que usa `U_GNA_AUR_Banner`)
- [ ] `EnsureUserFieldAsync` swallows excepciones con `LogWarning` en lugar de relanzar — revisar si es intencional
- [ ] `LogisticsCategorySyncUseCaseLoggingDecorator` y `ProductBrandsSyncUseCaseDecorator` existen pero los use cases subyacentes son stubs
- [ ] Añadir jobs de Quartz para `LogisticsCategory` y `ProductBrands` cuando los use cases estén implementados

---

## Convenciones del proyecto (resumen rápido)

- Clases: siempre `sealed` si no heredan
- DTOs/entidades: propiedades con `init` y `required` donde aplica
- Colecciones vacías: `[]` o `Array.Empty<T>()`
- Un tipo público por archivo
- No `var` si el tipo no es obvio en la misma línea
- Constantes SAP (endpoints, campos, literales) solo en `Infrastructure/ServiceLayer/Constants/`
- Constantes de dominio (nombres lógicos) solo en `Domain/Constants/ReplicationSchemaConstants.cs`
- Tests: siempre mock de interfaces, nunca de clases concretas; sin strings literales (usar constantes del dominio)
- Logging: `ILogger<T>` de `Microsoft.Extensions.Logging`, nunca `Serilog.ILogger` en Application/Domain

---

## Historial de cambios (actualizar al implementar algo nuevo)

| Fecha | Cambio |
|---|---|
| 2026-08-10 | Creación de este documento. Estado inicial del proyecto documentado. |
| — | `ArticleSyncUseCase` implementado completamente con flujo create/update/mark |
| — | `EnsureReplicationSchemaUseCase` implementado: UDTs REP_QUEUE, REP_ATTEMPT, CATLOG, MARCAS + UDFs en OITM |
| — | `ServiceLayerClient` y `AuroraArticleApiClient` implementados con Polly |
| — | Schema bootstrap (`SchemaBootstrapperHostedService`) integrado al arranque |
| — | `ArticleServiceLayerLookupRepository` implementado con batching OData |
| — | `ReplicationControlStore` implementado con paginación y manejo de duplicados |
| — | SP HANA `SP_GNAEA_ENQUEUE_ARTICLE_REPLICATION` + hook `SBO_SP_TransactionNotification` creados |
| — | Tests de `ArticleSyncUseCase` (5 tests) implementados |
| — | UDOs `CatLog` y `Marcas` provisionados automáticamente |
