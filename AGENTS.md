# Guía para agentes

## Mapa y fuentes del proyecto
- `db/` contiene PostgreSQL, el SQL de inicialización y sus seeds; `backend/` es el monolito modular .NET 10; `frontend/` es React + TypeScript + Vite. Cada componente tiene su propio Compose y comparte la red Docker `gestion-honorarios-net`.
- En backend, `src/GestionIngresosHonorarios.Api` configura HTTP y composición, `Application` coordina casos de uso, `Domain` contiene entidades/reglas, e `Infrastructure` implementa EF Core, Identity y PostgreSQL. Las migraciones EF viven en `backend/src/GestionIngresosHonorarios.Infrastructure/Data/Migrations`.
- En frontend, `src/main.tsx` monta la app, `src/App.tsx` declara rutas y `src/services/` concentra acceso HTTP. Usa `apiClient`; no crees clientes Axios paralelos. El access token vive en memoria y el refresh usa cookie HttpOnly; un `401` puede renovar una sola vez, un `403` no.
- Para el alcance público vigente, consulta `docs/PRD_GESTION_INGRESOS_HONORARIOS.md`; `docs/PRD_GESTION_INGRESOS_HONORARIOS_V0.11.md` amplía ese alcance con las liquidaciones privadas iniciales de Sanatorio Alemán. Para excepciones aprobadas del MVP, `docs/IMPLEMENTATION_DECISIONS.md` prevalece sobre recomendaciones genéricas de `docs/*_ARCHITECTURE_STANDARD.md`. Conserva como pendientes las decisiones marcadas «Por definir» en el PRD vigente.

## Reglas que condicionan cambios
- El mes es un workspace, no un CRUD por registro. Las horas públicas son enteros `>= 1`, pueden repetirse y no requieren fecha. El backend actualiza detalle y agregados mensuales en una transacción; CLP es entero y la retención pública se redondea por institución con `AwayFromZero`. Para liquidaciones privadas de Sanatorio Alemán, el bruto proviene de `TOTAL LIQUIDACIÓN` y la retención se redondea por PDF. No aceptes totales editables del cliente ni cambies silenciosamente snapshots/versiones históricas.
- `PROFESIONAL` solo accede a sus datos y solo su propietario crea/versiona sus tarifas; `ADMINISTRADOR` puede consultar globalmente, pero no escribir tarifas personales. La retención legal global se configura mediante seed/migración, no desde la API.
- Antes de importar una liquidación privada, valida el RUT del cobrador contra el perfil. Un pagador desconocido se rechaza; los RUT pagadores se agregan mediante migración/seed y el profesional vuelve a cargar el PDF.
- Las cuentas las crea Administración; no hay registro público, recuperación autónoma ni MFA en este MVP. No heredes esos flujos de los estándares genéricos sin una decisión de producto nueva.
- En autenticación frontend conserva el flujo centralizado existente de cookie refresh, token antifalsificación y token de acceso solo en memoria. Las rutas protegidas de React son UX: la autorización real y la propiedad del recurso se validan en backend.

## Comandos
- Frontend: requiere Node 24 (`frontend/.nvmrc`, `engines`). Desde `frontend/`: `npm ci`, `npm run lint`, `npm run typecheck`, `npm run build`. `package.json` no define un script de pruebas.
- Backend: usa el SDK fijado en `backend/global.json` (10.0.302). Desde `backend/`:
  ```powershell
  dotnet tool restore
  dotnet build GestionIngresosHonorarios.sln -c Release
  dotnet test GestionIngresosHonorarios.sln -c Release
  ```
- Para una prueba unitaria enfocada, desde `backend/`: `dotnet test tests/GestionIngresosHonorarios.UnitTests/GestionIngresosHonorarios.UnitTests.csproj -c Release --filter "FullyQualifiedName~IncomeCalculatorTests"`. Las pruebas con categoría `Integration` usan Testcontainers con PostgreSQL 17 y requieren Docker.
- Para agregar migración, desde `backend/`: `dotnet ef migrations add <Nombre> --project src/GestionIngresosHonorarios.Infrastructure --startup-project tools/PasswordHashTool --configuration Release`.
- Para el servidor local sin Compose, configura el archivo ignorado `backend/src/GestionIngresosHonorarios.Api/appsettings.Development.json` a partir de su `.example`, inicia PostgreSQL y desde `backend/` ejecuta `dotnet watch --project .\src\GestionIngresosHonorarios.Api\GestionIngresosHonorarios.Api.csproj run`.

## Base de datos y Compose
- Arranca los Compose en este orden, desde la raíz:
  ```powershell
  docker compose --env-file db/.env -f db/docker-compose.yaml up -d
  docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build
  docker compose -f frontend/docker-compose.yaml up -d --build
  ```
- `db/001_mvp_schema.sql` es el baseline de instalación y debe seguir alineado con la migración inicial EF; los cambios posteriores del esquema son migraciones versionadas en `backend/`. No editar directamente instalaciones existentes ni aplicar migraciones automáticamente al arrancar producción: usar el procedimiento explícito de despliegue.
- Los PDF privados se guardan en el volumen Docker persistente del backend. El producto no incluye respaldo fuera de Docker; conserva el volumen y no lo elimines salvo confirmación expresa.
- SQL y seeds de `db/` se ejecutan solo cuando PostgreSQL inicializa un volumen vacío. No elimines el volumen salvo que se haya confirmado que es un entorno local descartable.
- Usa `.env.example` y `appsettings.Development.example.json` como plantillas; no leas, copies a Git ni expongas `.env`, hashes, contraseñas o llaves locales.
