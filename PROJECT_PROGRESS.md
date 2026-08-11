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

**Flujo central (Outbound) — Órdenes de Compra (Alta + Modificación de líneas + Cancelación):**
1. SAP B1 enola una Orden de Compra (DocEntry) en `@GNA_AUR_REP_QUEUE` con `EntityType = 'PurchaseOrder'`, disparado por `SP_GNAEA_ENQUEUE_PURCHASEORDER_REPLICATION` (llamado desde `SBO_SP_TransactionNotification`, `object_type = '22'`, `transaction_type` `A`, `U` o `C`).
2. El job de Quartz (`PurchaseOrdersSyncJob`) llama a `PurchaseOrderSyncUseCase`.
3. El use case lee los DocEntry pendientes y obtiene la OC completa (header + `Cancelled` + `DocumentLines`) desde Service Layer (`PurchaseOrders`).
4. Rama de decisión (por prioridad):
   - **`PurchaseOrder.Cancelled == true`** → cancela la OC en Aurora (`DELETE /aurora-erp/purchase-orders/{externalId}`), o no hace nada si nunca llegó a existir allí (no-op exitoso). Tiene prioridad sobre Alta/Modificación sin importar con qué `Operation` haya quedado encolada la entrada — el flag `Cancelled` de SAP es la fuente de verdad, no el valor histórico de la cola.
   - **No cancelada y no existe en Aurora** (`GET /aurora-erp/purchase-orders/{externalId}`) → la crea completa con `POST /aurora-erp/purchase-orders`.
   - **No cancelada y ya existe** → como Aurora no expone un PATCH de header para `purchase-orders` (a diferencia de `sale-orders`), se **reconcilian las líneas** contra `GET .../articles`: agrega las nuevas (`POST .../articles`), edita las que cambiaron de cantidad (`PATCH .../articles/{sku}`) y elimina las que ya no están en SAP (`DELETE .../articles/{sku}`). Las líneas con `fulfilledQuantity > 0` en Aurora nunca se editan ni se eliminan (solo se loguea una advertencia).
5. Marca la OC como `REPLICATED` o `FAILED` en la cola con historial en `@GNA_AUR_REP_ATTEMPT`.
6. **Pendiente (backlog):** sincronización de campos de header (`bannerName`/`bannerExternalId`/`notes` — Aurora no expone endpoint para esto en `purchase-orders`), Reporte de ingreso (Aurora → SAP) y Aviso de cambio de estado (Aurora → SAP) — ver circuito completo en el doc del proyecto "Integración SAP B1 - Aurora Etapa1 CIRCUITOS".

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
│       │   ├── SapB1ProductBrandsConstants.cs   ← recurso U_GNA_AUR_MARCAS (Code/Name)
│       │   ├── SapB1BannersConstants.cs         ← recurso U_GNA_AUR_BANNERS (Code/Name)
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
    ├── ArticleSyncUseCaseTests.cs               ← 7 tests (todos implementados)
    ├── PurchaseOrderSyncUseCaseTests.cs         ← 9 tests (todos implementados)
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
| `GNA_AUR_CatLog` | `U_GNA_AUR_CatLog` | Alpha(30) | Categoría Logística (linked a GNA_AUR_CATLOG — OITM guarda el `Code`, no el `Name`) |
| `GNA_AUR_Marca` | `U_GNA_AUR_Marca` | Alpha(30) | Marca (linked a GNA_AUR_MARCAS — OITM guarda el `Code`, no el `Name`) |
| `GNA_AUR_IsBulky` | `U_GNA_AUR_IsBulky` | Alpha(1) Y/N | Es Voluminoso (default "N") |
| `GNA_AUR_IsCaged` | `U_GNA_AUR_IsCaged` | Alpha(1) Y/N | Es Enjaulado (default "N") |
| `GNA_AUR_Banner` | `U_GNA_AUR_Banner` | Alpha(30) | Banner (linked a GNA_AUR_BANNERS — OITM guarda el `Code`, no el `Name`. Cambiado de texto libre Alpha(150) a LinkedTable el 2026-08-11) |

> **✅ Corregido (2026-08-11):** `SapB1ItemsConstants.Items` tenía 3 constantes que apuntaban a nombres
> de campo que `EnsureReplicationSchemaUseCase` nunca provisionó (`U_GNA_AUR_BannerID`, `U_GNA_AUR_BrandID`,
> `U_GNA_AUR_CategoryName`) — el bug que señalaba la nota anterior de esta sección. Ahora apuntan a los
> UDFs reales: `BannerField = U_GNA_AUR_Banner`, `ProductBrandField = U_GNA_AUR_Marca`,
> `LogisticsCategoryField = U_GNA_AUR_CatLog`. Ver también el bug relacionado de deserialización JSON
> corregido en la sección "Entidad `Article`" más abajo.
>
> **⚠️ Cambio de diseño (2026-08-11):** `U_GNA_AUR_Banner` dejó de ser texto libre y pasó a ser
> LinkedTable contra `GNA_AUR_BANNERS`, igual patrón que CatLog/Marca (Code ≤8 chars en OITM, Name
> resuelto vía lookup). El cambio en `EnsureReplicationSchemaUseCase` (tamaño de campo 150→30 +
> `linkedTable: ReplicationSchemaConstants.BannersTable.Name`) ya estaba hecho en el working
> tree sin commitear al momento de este ticket; se confirmó con el usuario que es intencional y
> se commitea junto con el resto de este cambio. `Article.BannerID`/`BannerName` y
> `ArticleServiceLayerLookupRepository` se implementaron ya asumiendo este comportamiento.

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
| `PurchaseOrderSyncUseCase` | `IPurchaseOrderSyncUseCase` | ✅ Implementado | Alta (idempotente vía GET previo) + Modificación por reconciliación de líneas (add/edit/remove, respeta `fulfilledQuantity`) + Cancelación (por flag `Cancelled`, con prioridad sobre Alta/Modificación) |
| `LogisticsCategorySyncUseCase` | `ILogisticsCategorySyncUseCase` | ⚠️ STUB | Solo devuelve valores dummy. Falta implementación real |
| `ProductBrandsSyncUseCase` | `IProductBrandsSyncUseCase` | ⚠️ STUB | Solo devuelve valores dummy. Falta implementación real |

---

## Entidad `Article` — campos utilizados vs. comentados

**Activos:**
- `Sku`, `Name`, `PrimaryEan`, `AdditionalEans[]`
- `CategoryName`, `BrandID`, `BrandName`, `BannerID`, `BannerName`
- `IsBulky`, `IsCaged`

**Comentados (disponibles en DTO pero no en uso):**
- `WeightInGr`, `HeightInCm`, `WidthInCm`, `LengthInCm`
- `Colour`, `Size`, `HasProductionBatch`, `HasDueDate`, `HasSerialNumber`, `IsConsumable`

**`BannerID` / `BannerName` / `BrandID` / `BrandName` (2026-08-11 — antes no se seteaban al
crear/actualizar artículos en Aurora, a pedido explícito del usuario):**
- Banner y Marca son estructuralmente idénticos: ambos son UDFs LinkedTable en OITM
  (`U_GNA_AUR_Banner` → `GNA_AUR_BANNERS`, `U_GNA_AUR_Marca` → `GNA_AUR_MARCAS`), así que OITM
  solo guarda el **Code** (≤8 chars), nunca el Name. (Banner pasó de texto libre a LinkedTable el
  mismo día — ver nota en "Esquema en SAP B1".)
- `BannerID` / `BrandID`: el Code tal cual lo devuelve Service Layer. Se mapean a
  `CreateAuroraArticleDto.BannerExternalId`/`BrandExternalId` (ídem en Update).
- `BannerName` / `BrandName`: el Name resuelto **explícitamente** — Service Layer no expone las
  UDFs LinkedTable como navigation properties OData, así que no hay `$expand`/cross-join de una
  sola query que traiga Items + Name de Marca/Banner juntos.
  `ArticleServiceLayerLookupRepository.GetNamesByCodeAsync` (método genérico, reutilizado para
  ambas tablas) resuelve Code → Name con **una consulta batcheada por tabla, por corrida** (no
  una por artículo): junta los Codes distintos del lote de Items ya traído y los resuelve en
  lotes de `FilterBatchSize` (20) contra `U_GNA_AUR_MARCAS`/`U_GNA_AUR_BANNERS`
  `?$filter=Code eq '...' or Code eq '...'`, igual patrón que usa `GetBySkuListAsync` para Items.
  En el caso común (≤20 valores distintos de cada tabla por corrida) esto agrega **dos consultas
  extra** al ciclo completo (una por Marca, una por Banner), no N+1. Nulo si el artículo no tiene
  marca/banner asignado o el Code no matcheó ninguna fila de la tabla correspondiente.
- **⚠️ Bug relacionado corregido en el mismo cambio:** `ServiceLayerItemDto` (DTO interno de
  `ArticleServiceLayerLookupRepository`) no tenía `[JsonPropertyName]` en ninguna de sus propiedades
  de UDF. Como `ServiceLayerClient` deserializa con `PropertyNamingPolicy = null` (match exacto,
  case-sensitive), los campos `U_GNA_AUR_*` nunca podían bindear contra propiedades como `BrandID`
  o `CategoryName` — quedaban silenciosamente en `null` aunque Service Layer sí los devolviera.
  Corregido agregando `[JsonPropertyName(SapB1ItemsConstants.Items.XxxField)]` explícito a cada
  propiedad de UDF del DTO.

---

## Entidad `PurchaseOrder`

`PurchaseOrder` (header) + `PurchaseOrderLine[]` (líneas), leídas desde el recurso `PurchaseOrders`
de Service Layer (incluye `DocumentLines` sin necesidad de `$expand`, igual que Orders/Invoices).

- `DocEntry` (clave natural — inmutable, se usa como `EntityKey` en la cola y como `externalId` en Aurora)
- `DocNum` (informativo, no se usa como clave)
- `Cancelled` (mapeado desde el campo estándar `OPOR.Cancelled` de Service Layer, "tYES"/"tNO" — ⚠️ verificar en `$metadata` que el nombre/tipo coincide con la versión de B1 en uso). Es la fuente de verdad que usa `PurchaseOrderSyncUseCase` para decidir la rama de Cancelación, con prioridad sobre Alta/Modificación
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
es indistinto si la entrada quedó encolada con `U_GNA_AUR_Operation = 'I'`, `'U'` o `'C'`.

**Cancelación de OC:** `PurchaseOrderSyncUseCase.CancelInAuroraAsync` se ejecuta cuando
`PurchaseOrder.Cancelled == true`, sin importar el `Operation` con el que haya quedado
encolada la entrada (tiene prioridad sobre Alta/Modificación):

| Situación | Acción en Aurora |
|---|---|
| OC cancelada en SAP y existe en Aurora | `DELETE /aurora-erp/purchase-orders/{externalId}` |
| OC cancelada en SAP pero nunca existió en Aurora | **No-op** — se marca `REPLICATED` igual, solo se loguea información |

`AuroraPurchaseOrderApiClient.CancelPurchaseOrderAsync` es idempotente: un 404 de Aurora
(ya cancelada/eliminada en una corrida anterior) no se trata como error.

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
- Detecta Add (`A`), Update (`U`) **y** Cancel (`C`) en `object_type = '22'` (Purchase Order / Orden de Compra)
- Key de Transaction Notification para OC = `DocEntry` (columna única, igual patrón que Items)
- Evita duplicados: solo encola si no hay un `PENDING` para el mismo DocEntry (no importa si el
  pendiente existente quedó como `I`, `U` o `C`: `PurchaseOrderSyncUseCase` siempre relee el
  estado completo de la OC en SAP —incluido el flag `Cancelled`— nunca aplica un delta)
- Inserta directamente en `@GNA_AUR_REP_QUEUE` con `EntityType = 'PurchaseOrder'` y
  `U_GNA_AUR_Operation = 'I'/'U'/'C'` según corresponda (valor solo informativo/auditoría:
  el use case de C# no lo lee para decidir el flujo, usa el flag `Cancelled` de SAP)
- ⚠️ Riesgo conocido (heredado del mismo patrón en `SP_GNAEA_ENQUEUE_ARTICLE_REPLICATION`):
  el `Code` generado (`'PO-' || DocEntry || timestamp`) puede superar los 8 caracteres
  alfanuméricos documentados como máximo para la PK de UDTs en `copilot-instructions.md`
  (sección 8). Verificar el ancho real provisionado para `Code` en `@GNA_AUR_REP_QUEUE`
  antes de ir a producción con volumen — afecta a ambos SP por igual.

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
| `ArticleSyncUseCaseTests.cs` | 7 tests | ✅ Completos (happy path, create/update, error handling, CT propagation, propagación de excepción + mapping de bannerName/brandExternalId/brandName en create y update) |
| `PurchaseOrderSyncUseCaseTests.cs` | 9 tests | ✅ Completos (5 mínimos + reconciliación add/edit/remove + omisión de líneas con `fulfilledQuantity > 0` + cancelación existente/no-existente en Aurora) |
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
- [x] ~~Cancelación de OC en SAP → Aurora~~ — implementado vía flag `PurchaseOrder.Cancelled` + `CancelPurchaseOrderAsync` (`DELETE /aurora-erp/purchase-orders/{externalId}`), ver sección "Entidad `PurchaseOrder`". ⚠️ Verificar en el Service Layer real que el campo `Cancelled` de `PurchaseOrders` se comporta como se documentó (no probado contra una instancia SAP B1 real todavía)
- [ ] Aviso de cambio de estado en OC (Aurora → SAP): requiere UDF en OPOR para reflejar estado/seguimiento (ver doc "Integración SAP B1 - Aurora Etapa1 CIRCUITOS")
- [ ] Reporte de ingreso (Aurora → SAP): entrada de mercancías cumpliendo la OC
- [ ] Definir si `bannerName`/`bannerExternalId`/`notes` de la OC deben mapearse desde algún campo/UDF de SAP (hoy se omiten; Aurora tampoco expone forma de actualizarlos post-creación en `purchase-orders`)
- [ ] Revisar redondeo de `Quantity` (decimal SAP → int Aurora) en `PurchaseOrderSyncUseCase.ToAuroraQuantity` si el cliente usa UoM con cantidades fraccionarias
- [ ] Validar con el cliente/Aurora el criterio de "línea ya cumplida" (`fulfilledQuantity > 0` bloquea edit/remove): confirmar que ese es el comportamiento deseado ante una modificación en SAP sobre una línea parcialmente recibida

### Menores / mejoras técnicas (de `copilot-instructions.md` sección 9)
- [x] ~~`BrandExternalId` está comentado en `ArticleSyncUseCase` mapping~~ — descomentado y con `BrandName`/`BannerName` agregados (2026-08-11), ver sección "Entidad `Article`"
- [x] ~~Verificar coherencia entre campos que lee `ArticleServiceLayerLookupRepository` (`U_GNA_AUR_BannerID`, etc.) y los UDFs provisionados por `EnsureReplicationSchemaUseCase`~~ — corregido (2026-08-11), ver nota en "Esquema en SAP B1" y sección "Entidad `Article`"
- [ ] `EnsureUserFieldAsync` swallows excepciones con `LogWarning` en lugar de relanzar — revisar si es intencional
- [ ] ⚠️ Sin probar contra una instancia SAP B1/Service Layer real: confirmar en `$metadata` que `U_GNA_AUR_MARCAS`/`U_GNA_AUR_BANNERS` exponen `Code`/`Name` como campos estándar de UDT y que el `$filter "or"` batcheado de `GetNamesByCodeAsync` funciona igual que el ya usado para Items
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
| 2026-08-11 | Cancelación de OC (SAP → Aurora) implementada: `PurchaseOrder.Cancelled` (mapeado desde `OPOR.Cancelled` vía Service Layer), `PurchaseOrderSyncUseCase.CancelInAuroraAsync` (prioridad sobre Alta/Modificación), `IAuroraPurchaseOrderApiClient.CancelPurchaseOrderAsync`/`AuroraPurchaseOrderApiClient` (`DELETE /aurora-erp/purchase-orders/{externalId}`, idempotente ante 404) |
| 2026-08-11 | `SP_GNAEA_ENQUEUE_PURCHASEORDER_REPLICATION` actualizado para aceptar `transaction_type = 'C'` (Cancel), setea `U_GNA_AUR_Operation = 'C'` |
| 2026-08-11 | 2 tests nuevos de cancelación agregados a `PurchaseOrderSyncUseCaseTests` (OC cancelada existente en Aurora, OC cancelada que nunca existió en Aurora) |
| 2026-08-11 | **Datos de artículos completados**, a pedido explícito del usuario: `Article.BannerID`/`BannerName` y `Article.BrandID`/`BrandName` ahora se setean al crear/actualizar en Aurora — antes se enviaban sin `bannerExternalId`/`bannerName`/`brandExternalId`/`brandName` |
| 2026-08-11 | Corregido `SapB1ItemsConstants.Items`: 3 constantes apuntaban a UDFs que `EnsureReplicationSchemaUseCase` nunca provisiona (bug preexistente, ya señalado como TODO). Renombradas y corregidas: `BannerField = U_GNA_AUR_Banner`, `ProductBrandField = U_GNA_AUR_Marca`, `LogisticsCategoryField = U_GNA_AUR_CatLog` |
| 2026-08-11 | Corregido bug de deserialización en `ServiceLayerItemDto`: faltaban `[JsonPropertyName]` en las propiedades de UDF, por lo que Service Layer nunca lograba bindear `U_GNA_AUR_*` contra el DTO (`PropertyNamingPolicy = null` exige match exacto) |
| 2026-08-11 | `U_GNA_AUR_Banner` pasó de texto libre (Alpha 150) a LinkedTable contra `GNA_AUR_BANNERS` (Alpha 30, igual patrón que CatLog/Marca) en `EnsureReplicationSchemaUseCase` — cambio hecho localmente por el usuario, confirmado como intencional y commiteado en este mismo cambio |
| 2026-08-11 | `ArticleServiceLayerLookupRepository.GetNamesByCodeAsync` agregado (método genérico, reemplaza el antes específico `GetBrandNamesByCodeAsync`): resuelve Code → Name contra `U_GNA_AUR_MARCAS` y `U_GNA_AUR_BANNERS` en una consulta batcheada por tabla por corrida (no N+1), reutilizando el patrón `$filter "or"` en lotes de 20 ya usado para Items. Nuevas constantes `SapB1ProductBrandsConstants` y `SapB1BannersConstants` |
| 2026-08-11 | 2 tests nuevos agregados a `ArticleSyncUseCaseTests` verificando el mapeo de `bannerExternalId`/`bannerName`/`brandExternalId`/`brandName` hacia Aurora en create y update |
