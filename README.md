# Gestión de ingresos por honorarios

MVP para profesionales de la salud que registran horas e ingresos mensuales en instituciones públicas. El workspace evita el flujo CRUD tradicional: las horas se editan en contexto y la API actualiza los totales dentro de la misma transacción.

## Componentes

- `db/`: PostgreSQL, baseline SQL, seeds y Compose de base de datos.
- `backend/`: monolito modular .NET 10 con capas API, Application, Domain e Infrastructure; pruebas y migraciones EF Core.
- `frontend/`: React + TypeScript, servido en producción por Nginx; workspace mensual, Dashboard, administración y Compose.
- `docs/`: PRD, estándares y decisiones de implementación.

Cada componente tiene su propio Docker Compose. La base crea la red `gestion-honorarios-net`; iniciar los servicios en este orden:

```powershell
docker compose --env-file db/.env -f db/docker-compose.yaml up -d
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build
docker compose -f frontend/docker-compose.yaml up -d --build
```

La interfaz queda en `http://localhost:8080`; Swagger de desarrollo en `http://localhost:5000/swagger`.

## Preparar el primer inicio

1. Copiar `db/.env.example` a `db/.env` y `backend/.env.example` a `backend/.env`.
2. Usar los mismos valores `APP_DB_USER` y `APP_DB_PASSWORD` en `db/.env` y en `ConnectionStrings__Default` de `backend/.env`.
3. Generar el hash y el nombre normalizado del administrador con el hasher de ASP.NET Core Identity:

   ```powershell
   docker compose --env-file backend/.env --profile tools -f backend/docker-compose.yaml run --rm password-hash
   ```

   El programa solicita el nombre de usuario y la contraseña temporal sin mostrarla mientras se escribe. Copiar el nombre, nombre normalizado y hash a `db/.env`. No versionar ese archivo ni guardar la contraseña en SQL. El usuario administrador debe cambiarla en su primer ingreso.

4. Iniciar los Compose en el orden anterior. El seed del administrador, los roles y las tasas se ejecutan solo cuando PostgreSQL inicializa un volumen vacío.

La tasa global oficial inicial se fija en `db/002_catalog_seed.sql`: **15,25 % para 2026** y **16,00 % para 2027**. No se modifica desde la API.

## Desarrollo y verificaciones

- Backend local sin contenedor: completar `src/GestionIngresosHonorarios.Api/appsettings.Development.json` (si no existe, copiar ahí el archivo `.example` desde el explorador o el editor) con `APP_DB_USER` y `APP_DB_PASSWORD` de `db/.env`; ajustar base y puerto si se personalizaron. El archivo local está excluido de Git. Con PostgreSQL arriba, desde `backend/` ejecutar `dotnet watch --project .\src\GestionIngresosHonorarios.Api\GestionIngresosHonorarios.Api.csproj run`; Swagger queda en `http://localhost:5000/swagger` y `/health/ready` comprueba la conexión.
- Backend: `dotnet tool restore`, `dotnet build GestionIngresosHonorarios.sln -c Release`, `dotnet test GestionIngresosHonorarios.sln -c Release` desde `backend/`. La prueba de integración crea un PostgreSQL efímero con Testcontainers y requiere Docker.
- Migraciones: `dotnet ef migrations add <Nombre> --project src/GestionIngresosHonorarios.Infrastructure --startup-project tools/PasswordHashTool --configuration Release` desde `backend/`. Aplicar migraciones mediante un procedimiento explícito; no se ejecutan automáticamente al iniciar producción.
- Frontend: Node.js 24, `npm ci`, `npm run lint`, `npm run typecheck`, `npm run build` desde `frontend/`.
- Para Vite con hot reload, iniciar el Compose de frontend con el perfil `dev`; configura su proxy `/api` hacia `http://api:5000`.

La cuenta se crea únicamente por Administrador. No hay registro público ni recuperación autónoma: el administrador restablece contraseñas. MFA no forma parte de esta versión y la excepción está documentada en `docs/IMPLEMENTATION_DECISIONS.md`.

Para producción, `backend/docker-compose.production.yaml` complementa el Compose base y monta la llave RS256 y el certificado de Data Protection como Docker secrets. Definir `JWT_SIGNING_KEY_FILE`, `DATA_PROTECTION_CERTIFICATE_FILE` y `DATA_PROTECTION_PRIVATE_KEY_FILE` en el entorno protegido del despliegue. Configurar los orígenes CORS exactos y usar un terminador TLS/ingress confiable; Nginx conserva `X-Forwarded-Proto` del ingress para que cookies antifalsificación mantengan `Secure`.
