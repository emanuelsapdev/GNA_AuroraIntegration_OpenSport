# GNA AuroraIntegration — Estado del Proyecto

> **Propósito de este archivo:** Servir como punto de partida para sesiones con IA.
> Leerlo completo al inicio de cada sesión evita explorar el código desde cero.
> **Actualizar este archivo cada vez que se implemente, cambie o elimine algo.**

---

## Descripción general

Servicio de integración entre **SAP B1** (vía Service Layer REST) y **Aurora WMS** (API REST).
Es un Worker Service de .NET 10 que corre como **Windows Service**, ejecuta sincronización de artículos
de forma programada (Quartz.NET) y expone endpoints mínimos de salud e ingesta de eventos.

**Flujo central (Outbound) — Artículos:**
1. SAP B1 enola un artículo en `@GNA_AUR_REP_QUEUE` (vía Stored Procedure disparado desde `SBO_SP_TransactionNotification`).
2. El job de Quartz (`ArticlesSyncJob`) llama a `ArticleSyncUseCase`.
3. El use case lee los SKUs pendientes de la cola, obtiene el `Article` completo desde Service Layer, y lo crea o actualiza en Aurora WMS.
4. Marca el artículo como `REPLICATED` o `FAILED` en la cola con historial en `@GNA_AUR_REP_ATTEMPT`.

**Flujo central (Outbound) — Órdenes de Compra (Alta + Modificación de líneas):**
1. SAP B1 enola una Orden de Compra (DocEntry) en `@GNA_AUR_REP_QUEUE` con `EntityType = 'PurchaseOrder'`, disparado por `SP_GNAEA_ENQUEUE_PURCHASEORDER_REPLICATION` (llamado desde `SBO_SP_TransactionNotification`, `object_type = '22'`, `transaction_type` `A` o `U`).
2. El job de Quartz (`PurchaseOrdersSyncJob`) llama a `PurchaseOrderSyncUseCase`.
3. El use case lee los DocEntry pendientes y obtiene la OC completa (header + `DocumentLines`) desde Service Layer (`PurchaseOrders`).
4. Chequea si la OC ya existe en Aurora (`GET /aurora-erp/purchase-orders/{externalId}`):
   - **No existe** → la crea completa con `POST /aurora-erp/purchase-orders`.
   - **Ya existe** → como Aurora no expone un PATCH de header para `purchase-orders` (a diferencia de `sale-orders`), se **reconcilian las líneas** contra `GET .../articles`: agrega las nuevas (`POST .../articles`), edita las que cambiaron de cantidad (`PATCH .../articles/{sku}`) y elimina las que ya no están en SAP (`DELETE .../articles/{sku}`). Las líneas con `fulfilledQuantity > 0` en Aurora nunca se editan ni se eliminan (solo se loguea una advertencia).
5. Marca la OC como `REPLICATED` o `FAILED` en la cola con historial en `@GNA_AUR_REP_ATTEMPT`.
6. **Pendiente (backlog):** Cancelación de OC, sincronización de campos de header (`bannerName`/`bannerExternalId`/`notes` — Aurora no expone endpoint para esto en `purchase-orders`), Reporte de ingreso (Aurora → SAP) y Aviso de cambio de estado (Aurora → SAP) — ver circuito completo en el doc del proyecto "Integración SAP B1 - Aurora Etapa1 CIRCUITOS".

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
│   │   ├── PurchaseOrder.cs                     ← header de OC (DocEntry, DocNum, banner*, notes, Lines)
│   │   ├── PurchaseOrderLine.cs                 ← línea de OC (LineOrder, ArticleSku, Quantity)
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
│   │   ├── UseCaseValidationException.cs
│   │   ├── PurchaseOrderAuroraApiException.cs
│   │   ├── PurchaseOrderNotFoundException.cs
│   │   └── PurchaseOrderRepositoryException.cs
│   └── Interfaces/
│       ├── IArticleLookupRepository.cs          ← leer Items de SAP B1
│       ├── IArticleReplicationRepository.cs     ← cola específica de Article
│       ├── IPurchaseOrderLookupRepository.cs    ← leer PurchaseOrders de SAP B1
│       ├── IPurchaseOrderReplicationRepository.cs ← cola específica de PurchaseOrder
│       ├── IReplicationControlStore.cs          ← store genérico de cola/intentos
│       └── ISchemaProvisioningService.cs        ← provisionar UDTs/UDFs/UDOs

├── GNA.AuroraIntegration.Application/
│   ├── DTOs/Aurora/
│   │   ├── AuroraArticleDto.cs                  ← respuesta GET de Aurora
│   │   ├── CreateAuroraArticleDto.cs            ← payload POST a Aurora (con DataAnnotations)
│   │   ├── GroupOfArticleDto.cs
│   │   ├── UpdateAuroraArticleDto.cs            ← payload PATCH a Aurora
│   │   ├── CreateAuroraPurchaseOrderDto.cs      ← payload POST purchase-orders (con DataAnnotations)
│   │   ├── PurchaseOrderArticleDto.cs           ← línea del payload (lineOrder/articleSku/quantity) — alta, add y edit de línea
│   │   ├── PurchaseOrderArticleStateDto.cs      ← respuesta GET .../articles (requestedQuantity/fulfilledQuantity, clave para la reconciliación)
│   │   └── AuroraPurchaseOrderDto.cs            ← respuesta GET purchase-orders/{externalId}
│   ├── Interfaces/
│   │   ├── IAuroraArticleApiClient.cs           ← contrato del cliente Aurora (Artículos)
│   │   ├── IAuroraPurchaseOrderApiClient.cs     ← contrato del cliente Aurora (Órdenes de Compra: alta + add/edit/remove de líneas)
│   │   └── IServiceLayerClient.cs               ← contrato del cliente Service Layer
│   ├── UseCases/
│   │   ├── EnsureReplicationSchemaUseCase.cs    ← provisiona UDTs/UDFs/UDOs al arrancar
│   │   ├── IEnsureReplicationSchemaUseCase.cs
│   │   └── Outbound/
│   │       ├── ArticleSyncUseCase.cs            ← IMPLEMENTADO Y FUNCIONAL
│   │       ├── PurchaseOrderSyncUseCase.cs      ← IMPLEMENTADO (Alta + reconciliación de líneas en Modificación)
│   │       ├── LogisticsCategorySyncUseCase.cs  ← STUB (devuelve dummy values)
│   │       ├── ProductBrandsSyncUseCase.cs      ← STUB (devuelve dummy values)
│   │       ├── Decorators/
│   │       │   ├── ArticleSyncUseCaseLoggingDecorator.cs
│   │       │   ├── PurchaseOrderSyncUseCaseLoggingDecorator.cs
│   │       │   ├── LogisticsCategorySyncUseCaseLoggingDecorator.cs
│   │       │   └── ProductBrandsSyncUseCaseDecorator.cs
│   │       └── Interfaces/
│   │           ├── IArticleSyncUseCase.cs
│   │           ├── IPurchaseOrderSyncUseCase.cs
│   │           ├── ILogisticsCategorySyncUseCase.cs
│   │           └── IProductBrandsSyncUseCase.cs
│   └── Validation/
│       ├── ArticlePayloadValidator.cs           ← valida DTOs de Aurora con DataAnnotations
│       ├── IArticlePayloadValidator.cs
│       ├── PurchaseOrderPayloadValidator.cs
│       └── IPurchaseOrderPayloadValidator.cs

├── GNA.AuroraIntegration.Infrastructure/
│   ├── Aurora/
│   │   ├── AuroraApiSettings.cs                 ← BaseUrl, ApiKey, Warehouse (TODO: origen del valor)
│   │   ├── AuroraArticleApiClient.cs            ← cliente HTTP Aurora Artículos (retry + circuit breaker)
│   │   └── AuroraPurchaseOrderApiClient.cs      ← cliente HTTP Aurora Órdenes de Compra (ídem)
│   ├── Repositories/
│   │   ├── ArticleReplicationRepository.cs      ← adapta IArticleReplicationRepository sobre el store genérico
│   │   ├── PurchaseOrderReplicationRepository.cs ← adapta IPurchaseOrderReplicationRepository sobre el store genérico
│   │   └── ReplicationControlStore.cs           ← IReplicationControlStore sobre UDTs SAP B1
│   ├── Requireds/
│   │   ├── SP_GNAEA_ENQUEUE_ARTICLE_REPLICATION.txt        ← SP HANA que encola artículos
│   │   ├── SP_GNAEA_ENQUEUE_PURCHASEORDER_REPLICATION.txt  ← SP HANA que encola OC (solo Alta, object_type 22)
│   │   └── SBO_SP_TransactionNotification.txt              ← hook SAP B1 que llama a ambos SP anteriores
│   └── ServiceLayer/
│       ├── Client/
│       │   ├── ServiceLayerClient.cs            ← cliente HTTP SL (session cookie B1SESSION, retry + CB)
│       │   └── ServiceLayerSettings.cs          ← BaseUrl, CompanyDB, UserName, Password
│       ├── Constants/
│       │   ├── SapB1ItemsConstants.cs           ← campos de Items (ItemCode, BarCode, U_GNA_AUR_*)
│       │   ├── SapB1PurchaseOrdersConstants.cs  ← recurso PurchaseOrders (DocEntry, DocNum, DocumentLines)
│       │   └── SapB1ReplicationConstants.cs     ← endpoints U_*, campos U_*, estados PENDING/REPLICATED/FAILED
│       ├── Mapping/
│       │   ├── SapYesNoMapper.cs
│       │   ├── UserFieldSubTypeMapper.cs
│       │   ├── UserFieldTypeMapper.cs
│       │   ├── UserObjectTypeMapper.cs
│       │   └── UserTableTypeMapper.cs
│       ├── Repositories/
│       │   ├── ArticleServiceLayerLookupRepository.cs  ← lee Items de SAP B1, mapea a Article
│       │   └── PurchaseOrderServiceLayerLookupRepository.cs ← lee PurchaseOrders (+DocumentLines) de SAP B1
│       └── Services/
│           └── ServiceLayerSchemaProvisioningService.cs ← ISchemaProvisioningService sobre Service Layer

└── GNA.AuroraIntegration.Host/
    ├── Health/
    │   └── ServiceLayerHealthCheck.cs           ← IHealthCheck para /health
    ├── Jobs/
    │   ├── ArticlesSyncJob.cs                   ← [DisallowConcurrentExecution] Quartz job
    │   └── PurchaseOrdersSyncJob.cs             ← [DisallowConcurrentExecution] Quartz job
    ├── Startup/
    │   └── SchemaBootstrapperHostedService.cs   ← corre EnsureReplicationSchemaUseCase al arrancar
    ├── Program.cs                               ← DI, Quartz, Serilog, Windows Service
    ├── appsettings.json                         ← estructura de config (vacía, se llena en Development)
    └── appsettings.Development.json             ← valores reales para desarrollo local

tests/
└── GNA.AuroraIntegration.Tests/
    ├── ArticleSyncUseCaseTests.cs               ← 5 tests (todos implementados)
    ├── PurchaseOrderSyncUseCaseTests.cs         ← 5 tests (todos implementados)
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
| `GNA_AUR_BANNERS` | `@GNA_AUR_BANNERS` | — | Banners (MasterData). Solo campos Code/Name por defecto, sin UDFs adicionales |

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
| `Banners` | `GNA_AUR_BANNERS` | Banners (menú FatherMenuID 11520, pos 16). Solo Code/Name por defecto |

---

## Campos de la cola `@GNA_AUR_REP_QUEUE`

| Campo SAP | Valores posibles |
|---|---|
| `U_GNA_AUR_EntityType` | `Article` / `PurchaseOrder` (más en el futuro — ver `ReplicableEntityType`) |
| `U_GNA_AUR_EntityKey` | SKU del artículo, o DocEntry (como string) para OC |
| `U_GNA_AUR_Operation` | `I` (Insert) / `U` (Update) — PurchaseOrder solo usa `I` por ahora |
| `U_GNA_AUR_Status` | `PENDING` / `REPLICATED` / `FAILED` |
| `U_GNA_AUR_RetryCount` | Número de reintentos (máx. 4 para Article y para PurchaseOrder) |

---

## Use Cases — estado actual

| Use Case | Interfaz | Estado | Notas |
|---|---|---|---|
| `EnsureReplicationSchemaUseCase` | `IEnsureReplicationSchemaUseCase` | ✅ Implementado | Provisiona toda la UDT/UDF/UDO al arrancar |
| `ArticleSyncUseCase` | `IArticleSyncUseCase` | ✅ Implementado | Flujo completo create/update con validación |
| `PurchaseOrderSyncUseCase` | `IPurchaseOrderSyncUseCase` | ✅ Implementado | Alta (idempotente vía GET previo) + Modificación por reconciliación de líneas (add/edit/remove, respeta `fulfilledQuantity`). Falta Cancelación |
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

## Entidad `PurchaseOrder`

`PurchaseOrder` (header) + `PurchaseOrderLine[]` (líneas), leídas desde el recurso `PurchaseOrders`
de Service Layer (incluye `DocumentLines` sin necesidad de `$expand`, igual que Orders/Invoices).

- `DocEntry` (clave natural — inmutable, se usa como `EntityKey` en la cola y como `externalId` en Aurora)
- `DocNum` (informativo, no se usa como clave)
- `BannerName` / `BannerExternalId` — **no mapeados aún** (opcionales en Aurora, sin campo SAP definido; y sin endpoint de Aurora para actualizarlos post-creación)
- `Notes` — no mapeado aún (misma limitación que arriba)
- `Lines[]`: `LineOrder` (POR1.LineNum), `ArticleSku` (POR1.ItemCode), `Quantity` (POR1.Quantity, decimal en SAP → se redondea a `int` al armar el payload de Aurora)

**Supuesto de diseño (a validar):** las líneas de la OC se envían a Aurora solo con
`lineOrder/articleSku/quantity` — **no** se incluye el objeto `article` embebido (alta de
artículo nuevo) que la API de Aurora admite como opcional. Se asume que el SKU ya fue
replicado por `ArticleSyncUseCase` antes de que la OC llegue a Aurora; si no es así, la
creación de la OC falla y queda en retry hasta que el artículo exista (o hasta agotar
`MaxRetryCounts.PurchaseOrder`).

**Modificación de OC — reconciliación de líneas:** cuando la OC ya existe en Aurora,
`PurchaseOrderSyncUseCase.ReconcileLinesAsync` compara `PurchaseOrder.Lines` (SAP, estado
completo actual) contra `GET .../articles` (Aurora) por `ArticleSku`:

| Situación | Acción en Aurora |
|---|---|
| SKU en SAP, no en Aurora | `POST .../articles` (alta de línea) |
| SKU en ambos, cantidad distinta, `fulfilledQuantity = 0` | `PATCH .../articles/{sku}` |
| SKU en ambos, cantidad distinta, `fulfilledQuantity > 0` | **Se omite** — solo `LogWarning` |
| SKU en Aurora, ya no está en SAP, `fulfilledQuantity = 0` | `DELETE .../articles/{sku}` |
| SKU en Aurora, ya no está en SAP, `fulfilledQuantity > 0` | **Se omite** — solo `LogWarning` |

La cola no aplica deltas: cada corrida relee el estado completo de la OC en SAP, así que
es indistinto si la entrada quedó encolada con `U_GNA_AUR_Operation = 'I'` o `'U'`.

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
    "ApiKey": "...",
    "Warehouse": "..."
  },
  "ServiceLayer": {
    "BaseUrl": "...",
    "CompanyDB": "...",
    "UserName": "...",
    "Password": "..."
  },
  "Jobs": {
    "ArticlesSyncJob": { "Cron": "0 * * * * ?" },
    "PurchaseOrdersSyncJob": { "Cron": "0 * * * * ?" }
  },
  "Serilog": { ... }
}
```

⚠️ `AuroraApi:Warehouse` es **TODO**: los endpoints de `purchase-orders` en Aurora
requieren el query param `warehouse` (a diferencia de `articles`, donde es opcional).
Por ahora `AuroraPurchaseOrderApiClient` lo toma de este único valor de configuración
(si está vacío, el request se envía sin el param y Aurora puede rechazarlo). Falta
definir con el cliente/Aurora si Open Sport opera con un único depósito o si hace
falta mapear por `WhsCode` de SAP.

Ambas `AuroraApiSettings` y `ServiceLayerSettings` tienen `.ValidateDataAnnotations().ValidateOnStart()` — el servicio falla al arrancar si falta configuración.

---

## Stored Procedures SAP B1 (HANA)

Ubicación en repo: `src/GNA.AuroraIntegration.Infrastructure/Requireds/`

### `SP_GNAEA_ENQUEUE_ARTICLE_REPLICATION`
- Se llama desde `SBO_SP_TransactionNotification`
- Detecta Add (`A`) / Update (`U`) en `object_type = '4'` (Items)
- Evita duplicados: solo encola si no hay un `PENDING` para el mismo SKU
- Inserta directamente en `@GNA_AUR_REP_QUEUE`

### `SP_GNAEA_ENQUEUE_PURCHASEORDER_REPLICATION`
- Se llama desde `SBO_SP_TransactionNotification`
- Detecta Add (`A`) **y** Update (`U`) en `object_type = '22'` (Purchase Order / Orden de Compra)
- Key de Transaction Notification para OC = `DocEntry` (columna única, igual patrón que Items)
- Evita duplicados: solo encola si no hay un `PENDING` para el mismo DocEntry (no importa si el
  pendiente existente quedó como `I` y ahora llega una `U`, o viceversa: `PurchaseOrderSyncUseCase`
  siempre relee el estado completo de la OC en SAP, nunca aplica un delta)
- Inserta directamente en `@GNA_AUR_REP_QUEUE` con `EntityType = 'PurchaseOrder'`
- ⚠️ Riesgo conocido (heredado del mismo patrón en `SP_GNAEA_ENQUEUE_ARTICLE_REPLICATION`):
  el `Code` generado (`'PO-' || DocEntry || timestamp`) puede superar los 8 caracteres
  alfanuméricos documentados como máximo para la PK de UDTs en `copilot-instructions.md`
  (sección 8). Verificar el ancho real provisionado para `Code` en `@GNA_AUR_REP_QUEUE`
  antes de ir a producción con volumen — afecta a ambos SP por igual.
- Cancelación de OC: **no implementada** (backlog)

### `SBO_SP_TransactionNotification`
- Hook estándar de SAP B1
- Llama a ambos SP (`SP_GNAEA_ENQUEUE_ARTICLE_REPLICATION` y `SP_GNAEA_ENQUEUE_PURCHASEORDER_REPLICATION`) y propaga error/mensaje

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
| `PurchaseOrderSyncUseCaseTests.cs` | 7 tests | ✅ Completos (5 mínimos + reconciliación add/edit/remove + omisión de líneas con `fulfilledQuantity > 0`) |
| `EnsureReplicationSchemaUseCaseTests.cs` | 13 tests | ✅ Completos (incluye tabla/UDO de Banners y verificación de que no se le agregan UDFs) |

---

## Pendientes / TODOs conocidos

### Críticos (bloquean funcionalidad completa)
- [ ] Implementar `LogisticsCategorySyncUseCase` real (lee de SAP B1 y sincroniza con Aurora)
- [ ] Implementar `ProductBrandsSyncUseCase` real
- [ ] Implementar endpoint `POST /events/status` con use case real (inbound desde Aurora)
- [ ] Definir origen de `AuroraApi:Warehouse` (fijo por configuración vs. mapeado desde `WhsCode` de SAP) — bloquea que `PurchaseOrderSyncUseCase` funcione contra Aurora real si el endpoint efectivamente exige el param

### Circuito de Órdenes de Compra — pendiente
- [x] ~~Modificación de OC / líneas de OC en SAP → Aurora~~ — implementado como reconciliación de líneas (`add`/`edit`/`remove`), ver sección "Entidad `PurchaseOrder`"
- [ ] Cancelación de OC en SAP → Aurora (`DELETE /aurora-erp/purchase-orders/{externalId}`)
- [ ] Aviso de cambio de estado en OC (Aurora → SAP): requiere UDF en OPOR para reflejar estado/seguimiento (ver doc "Integración SAP B1 - Aurora Etapa1 CIRCUITOS")
- [ ] Reporte de ingreso (Aurora → SAP): entrada de mercancías cumpliendo la OC
- [ ] Definir si `bannerName`/`bannerExternalId`/`notes` de la OC deben mapearse desde algún campo/UDF de SAP (hoy se omiten; Aurora tampoco expone forma de actualizarlos post-creación en `purchase-orders`)
- [ ] Revisar redondeo de `Quantity` (decimal SAP → int Aurora) en `PurchaseOrderSyncUseCase.ToAuroraQuantity` si el cliente usa UoM con cantidades fraccionarias
- [ ] Validar con el cliente/Aurora el criterio de "línea ya cumplida" (`fulfilledQuantity > 0` bloquea edit/remove): confirmar que ese es el comportamiento deseado ante una modificación en SAP sobre una línea parcialmente recibida

### Menores / mejoras técnicas (de `copilot-instructions.md` sección 9)
- [ ] `BrandExternalId` está comentado en `ArticleSyncUseCase` mapping (líneas 104 y 117). Definir si Aurora lo requiere
- [ ] Verificar coherencia entre campos que lee `ArticleServiceLayerLookupRepository` (`U_GNA_AUR_BannerID`, etc.) y los UDFs provisionados por `EnsureReplicationSchemaUseCase` (que usa `U_GNA_AUR_Banner`)
- [ ] `EnsureUserFieldAsync` swallows excepciones con `LogWarning` en lugar de relanzar — revisar si es intencional
- [ ] `LogisticsCategorySyncUseCaseLoggingDecorator` y `ProductBrandsSyncUseCaseDecorator` existen pero los use cases subyacentes son stubs
- [ ] Añadir jobs de Quartz para `LogisticsCategory` y `ProductBrands` cuando los use cases estén implementados
- [ ] Verificar ancho real de la columna `Code` en `@GNA_AUR_REP_QUEUE` vs. el `Code` largo generado por ambos SP de encolado (ver nota en sección de Stored Procedures)

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
| 2026-08-10 | `PurchaseOrderSyncUseCase` implementado (Etapa 1: solo Alta, chequeo de idempotencia vía GET, sin objeto `article` embebido en las líneas) |
| 2026-08-10 | `PurchaseOrder`/`PurchaseOrderLine` (Domain), DTOs y `IAuroraPurchaseOrderApiClient`/`AuroraPurchaseOrderApiClient` (Aurora `purchase-orders`) creados |
| 2026-08-10 | `PurchaseOrderServiceLayerLookupRepository` implementado sobre el recurso `PurchaseOrders` (+`DocumentLines`) de Service Layer, con batching OData |
| 2026-08-10 | `PurchaseOrderReplicationRepository` implementado sobre el store genérico (`ReplicableEntityType.PurchaseOrder`, ya existía en el enum) |
| 2026-08-10 | SP HANA `SP_GNAEA_ENQUEUE_PURCHASEORDER_REPLICATION` (object_type 22, solo Alta) + `SBO_SP_TransactionNotification` actualizado para llamarlo |
| 2026-08-10 | `PurchaseOrdersSyncJob` (Quartz) registrado en `Program.cs` junto con toda la DI del circuito de OC |
| 2026-08-10 | `AuroraApiSettings.Warehouse` agregado (TODO: definir origen del valor — ver Pendientes) |
| 2026-08-10 | Tests de `PurchaseOrderSyncUseCase` (5 tests) implementados |
| 2026-08-10 | Modificación de OC: `PurchaseOrderSyncUseCase.ReconcileLinesAsync` agregado — reconcilia líneas SAP vs. Aurora (add/edit/remove), respetando `fulfilledQuantity > 0` |
| 2026-08-10 | `IAuroraPurchaseOrderApiClient`/`AuroraPurchaseOrderApiClient` extendidos con `GetPurchaseOrderArticlesAsync`/`AddPurchaseOrderArticlesAsync`/`UpdatePurchaseOrderArticleAsync`/`RemovePurchaseOrderArticleAsync` |
| 2026-08-11 | `EnsureReplicationSchemaUseCase` extendido: nueva UDT `GNA_AUR_BANNERS` (MasterData, solo Code/Name por defecto, sin UDFs) + UDO `Banners` (menú FatherMenuID 11520, pos 16), a pedido explícito del usuario. Tests actualizados (3 nuevos, total de operaciones 20 → 22) |
| 2026-08-10 | `PurchaseOrderArticleStateDto` agregado (mapea la respuesta de `GET .../articles`) |
| 2026-08-10 | `SP_GNAEA_ENQUEUE_PURCHASEORDER_REPLICATION` actualizado para aceptar `transaction_type = 'U'` además de `'A'` |
| 2026-08-10 | 2 tests nuevos de reconciliación agregados a `PurchaseOrderSyncUseCaseTests` (add/edit/remove, y omisión de líneas cumplidas) |
