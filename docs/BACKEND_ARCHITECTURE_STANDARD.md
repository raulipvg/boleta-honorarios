# Estándar de arquitectura: Backend ASP.NET Core (.NET)

Este documento define una arquitectura de referencia para backends de nuevos proyectos y sirve como contexto para desarrolladores y agentes de IA. Toma como referencia tecnológica `TMS/tms-backend-web`, pero establece controles de seguridad y límites de capas para proyectos nuevos; no implica que el TMS actual ya implemente todos los controles aquí descritos.

Los nombres `<Proyecto>`, `<modulo>`, `<entidad>` y `<operacion>` son placeholders estructurales. No representan funcionalidades que deban generarse automáticamente.

## 1. Stack tecnológico

- **Plataforma:** C# sobre .NET 10 LTS y ASP.NET Core Web API.
- **Transporte:** API REST sobre HTTP/JSON mediante controllers.
- **Persistencia de referencia:** PostgreSQL con Entity Framework Core 10 y el proveedor Npgsql.
- **Autenticación API:** access token JWT Bearer de corta duración y refresh token opaco en cookie `HttpOnly`.
- **Documentación API:** OpenAPI y Swagger, habilitados según ambiente.
- **Contenedores:** Docker y Docker Compose, con imagen multi-stage `dotnet/sdk` → `dotnet/aspnet`.
- **Seguridad:** OWASP Top 10:2025, OWASP API Security Top 10:2023 y OWASP ASVS 5.0.0.

El TMS de referencia apunta a `net10.0`, EF Core `10.0.10` y Npgsql `10.0.3`. Cada proyecto nuevo registra versiones soportadas y compatibles en `global.json`, `Directory.Packages.props` y/o los archivos de proyecto; actualiza parches de seguridad sin cambiar versiones de forma silenciosa. El target framework y la versión de cada paquete se confirman al iniciar el proyecto.

El objetivo de verificación recomendado es OWASP ASVS 5.0.0 nivel 2 para aplicaciones que procesen credenciales, datos personales o información de negocio. Los sistemas de alto impacto deben definir si necesitan nivel 3 y pruebas independientes adicionales. OWASP Top 10 es una base de priorización, no una certificación.

## 2. Principios arquitectónicos

- Empezar como monolito modular, separado por proyectos/capas y funcionalidades; desplegar microservicios solo ante necesidades operativas concretas.
- Derivar módulos, entidades, endpoints y reglas de los requisitos reales.
- Mantener la API como punto de entrada HTTP y ubicar reglas de negocio fuera de los controllers.
- Mantener el dominio independiente de ASP.NET Core, EF Core y proveedores externos.
- Usar DTOs explícitos para entrada y salida; no enlazar entidades EF directamente desde el cliente.
- Validar los límites de entrada y autorizar cada operación y recurso en el servidor.
- Aplicar mínimo privilegio, denegación por defecto y manejo seguro de errores.
- Añadir abstracciones cuando reduzcan acoplamiento real o faciliten una frontera de integración; no crear capas, repositorios genéricos ni módulos vacíos por plantilla.
- Tratar las decisiones de seguridad como requisitos funcionales verificables, no como tareas posteriores.

## 3. Estructura recomendada de la solución

```text
<Proyecto>.sln
├── global.json
├── Directory.Build.props                 # opcional: convenciones comunes
├── Directory.Packages.props              # opcional: versiones centralizadas
├── src/
│   ├── <Proyecto>.Api/
│   │   ├── Controllers/
│   │   ├── Middleware/
│   │   ├── Authorization/
│   │   ├── Realtime/                      # opcional: SignalR
│   │   ├── Program.cs
│   │   └── appsettings*.json              # sin secretos
│   ├── <Proyecto>.Application/
│   │   ├── Features/
│   │   │   └── <modulo>/
│   │   │       ├── DTOs/
│   │   │       ├── Interfaces/
│   │   │       └── Services/              # o casos de uso cuando aporte valor
│   │   ├── Configuration/
│   │   ├── Exceptions/
│   │   └── Common/
│   ├── <Proyecto>.Domain/
│   │   ├── Entities/
│   │   ├── ValueObjects/                  # solo si existen conceptos del dominio
│   │   ├── Enums/
│   │   └── Exceptions/
│   └── <Proyecto>.Infrastructure/
│       ├── Data/
│       ├── Persistence/
│       ├── Identity/
│       ├── Integrations/
│       ├── Repositories/                  # solo si hay una frontera útil
│       └── DependencyInjection.cs
└── tests/
    ├── <Proyecto>.UnitTests/
    ├── <Proyecto>.IntegrationTests/
    └── <Proyecto>.ApiTests/
```

### Referencias entre proyectos

```text
Api → Application → Domain
Api → Infrastructure → Application / Domain
```

- `Domain` no referencia otros proyectos de la solución.
- `Application` referencia `Domain` y declara contratos que requiere.
- `Infrastructure` implementa persistencia e integraciones detrás de esos contratos.
- `Api` es el composition root: registra implementaciones de `Infrastructure` y servicios de `Application`.
- Un acceso directo desde `Application` a EF Core puede aceptarse como excepción explícita de un MVP, pero no debe arrastrar entidades EF al contrato HTTP ni convertirse en una dependencia sin evaluar sus costes.

No todos los directorios del ejemplo se crean si el módulo no los necesita.

## 4. Responsabilidades

| Componente | Responsabilidad |
|---|---|
| `Program.cs` | Configura DI, autenticación, autorización, CORS, antiforgery, límites, middleware y pipeline. |
| Controller | Recibe HTTP, aplica políticas, enlaza/valida DTOs y traduce resultados a códigos y respuestas HTTP. No contiene consultas ni reglas de negocio. |
| Application service/caso de uso | Coordina una operación, valida precondiciones de aplicación, aplica reglas y utiliza contratos de persistencia/integración. |
| DTO | Expone solamente los campos permitidos en cada operación. Evita over-posting/mass assignment y filtración de entidades. |
| Domain | Modela conceptos, invariantes y reglas de negocio independientes del transporte y persistencia. |
| `DbContext` y configuraciones EF | Mapean entidades, índices, relaciones, filtros y transacciones en `Infrastructure`. |
| Repositorio/puerto | Aísla una frontera útil o consulta especializada; no debe ser una copia mecánica de `DbSet`. |
| Middleware/filtro | Gestiona funciones HTTP transversales, como Problem Details, correlación y antiforgery donde aplique. |
| Configuración | Lee y valida opciones tipadas; nunca contiene secretos en archivos versionados. |

## 5. Flujo normal de una solicitud

```text
HTTP Request
  → autenticación (si corresponde)
  → autorización de función y recurso
  → binding + validación del DTO
  → controller
  → caso de uso / servicio de Application
  → contrato de Application
  → implementación de Infrastructure / EF Core
  → PostgreSQL o integración externa
  → DTO de respuesta
  → HTTP Response
```

- Toda operación que recibe un identificador comprueba que el usuario puede acceder a ese objeto; no basta con autenticar al usuario.
- Consultas y transacciones deben limitar datos y filas en el servidor, usar paginación con límites y soportar cancelación mediante `CancellationToken`.
- Las transacciones agrupan únicamente cambios que deban ser atómicos.
- Usar LINQ parametrizado de EF Core. El SQL crudo requiere parámetros; nunca concatenar entradas para formar SQL o comandos de sistema.
- Proyectar a DTOs y seleccionar solo propiedades necesarias. No aceptar entidades de dominio/EF completas desde el body.

## 6. Módulos y evolución

- Los módulos se definen a partir del dominio real; cada uno posee sus contratos HTTP, DTOs y servicios/casos de uso correspondientes.
- Un controller delega en Application y mantiene el detalle HTTP en la capa API.
- Un módulo no consume directamente clases internas de otro módulo. Definir colaboración explícita cuando haya una dependencia real.
- `Common` contiene solo componentes técnicos transversales y reutilizados; no es un cajón para lógica de negocio.
- Añadir dominio más rico, repositorios, casos de uso explícitos o mensajería cuando existan reglas, transacciones, integraciones o necesidades de prueba que lo justifiquen.
- Las abstracciones evolucionan módulo por módulo, no mediante una reorganización masiva anticipada.

## 7. Autenticación y funcionamiento del login

### 7.1 Componentes y contrato

Si el proyecto administra credenciales propias, usar ASP.NET Core Identity o un proveedor de identidad reconocido (OIDC) en vez de inventar gestión de usuarios, hashing, MFA y recuperación. La emisión de access/refresh tokens se coordina desde un servicio de autenticación con límites claros.

Contrato propuesto para SPA React + API .NET:

| Método | Endpoint | Función |
|---|---|---|
| `GET` | `/api/auth/csrf` | Emite/prepara el token antifalsificación de la sesión anónima. |
| `POST` | `/api/auth/login` | Verifica credenciales; si corresponde, inicia el desafío MFA. En éxito emite access token y establece cookie refresh. |
| `POST` | `/api/auth/mfa/verify` | Completa un desafío MFA de corta vida. |
| `POST` | `/api/auth/refresh` | Valida y rota el refresh cookie; devuelve un nuevo access token. |
| `POST` | `/api/auth/logout` | Revoca la sesión actual y expira la cookie. |
| `POST` | `/api/auth/logout-all` | Revoca todas las sesiones del usuario; requiere autenticación y antiforgery. |
| `GET` | `/api/auth/me` | Devuelve la identidad mínima del usuario autenticado. |
| `POST` | `/api/auth/forgot-password` | Inicia recuperación sin revelar si la cuenta existe. |
| `POST` | `/api/auth/reset-password` | Consume un token de recuperación de un solo uso. |

El refresh token no se devuelve en JSON ni se expone a JavaScript. El access token es Bearer, vive poco tiempo y permanece en memoria en el cliente. Las llamadas que usan refresh/logout incluyen cookie y token antifalsificación.

### 7.2 Secuencia de login

1. La SPA solicita `/api/auth/csrf` y conserva el token antifalsificación en memoria.
2. El usuario envía identificador y contraseña a `/api/auth/login` por HTTPS con `X-CSRF-TOKEN` y el origen permitido.
3. ASP.NET aplica límites de intentos antes de ejecutar el hash de contraseña. Normaliza el identificador según su tipo, pero no recorta ni transforma la contraseña.
4. ASP.NET Core Identity/proveedor verifica la contraseña. Para una cuenta inexistente se usa una verificación de coste comparable para reducir diferencias de tiempo que faciliten enumeración.
5. Credenciales inválidas, cuenta inexistente o no habilitada producen una respuesta genérica que no revela cuál condición falló. Los detalles internos se registran sin guardar credenciales.
6. Para cuentas administrativas, cuentas de riesgo y acciones de alto impacto se exige MFA. Si falta el segundo factor, responder con un desafío corto, de un solo uso y sin emitir acceso completo ni refresh cookie.
7. Tras completar factores, crear una sesión independiente. Generar el refresh secreto con CSPRNG; persistir solo su hash, identificador de familia, expiración, estado y metadatos mínimos de auditoría.
8. Emitir access JWT corto (baseline configurable de 5–15 minutos), sin datos sensibles; validar algoritmo permitido, firma, issuer, audience, `sub`, `iat` y `exp`.
9. Responder con el access token y establecer una cookie host-only `__Host-RefreshToken`: `HttpOnly`, `Secure` en todos los despliegues HTTPS, `Path=/`, sin `Domain`, `SameSite=Strict` o `Lax` según los flujos y expiración conforme a la política de sesión.
10. La SPA guarda el access token solo en memoria y carga la identidad mediante `/api/auth/me`. El backend sigue verificando permisos en cada petición.

### 7.3 Renovación, logout y ciclo de vida

- Al iniciar o recargar la SPA, llamar a `/api/auth/refresh` con cookie y antiforgery; si funciona, reconstruir el estado de identidad. No leer ni persistir refresh tokens en el navegador.
- Rotar refresh token en cada renovación mediante una transacción atómica. Detectar reutilización de un token rotado, revocar su familia y generar un evento de seguridad.
- El frontend comparte una sola renovación concurrente y coordina pestañas para evitar carreras de rotación.
- Al recibir `401`, intentar refresh una sola vez y reintentar la petición original una sola vez. Un `403` nunca inicia refresh.
- Logout revoca la sesión actual en servidor y expira la cookie con los mismos atributos; el frontend limpia el estado en memoria.
- Contraseñas cambiadas/restablecidas y eventos de riesgo invalidan las sesiones afectadas. Definir expiración idle y absoluta según impacto y riesgo.
- Documentar el intervalo residual de validez de un JWT ya emitido. Para revocación inmediata de acciones de alto riesgo, validar estado/versión de sesión; en otros casos usar expiración corta y revocar refresh.
- No vincular rígidamente cada sesión a IP o User-Agent; usarlos solo como señales de riesgo, evitando falsos positivos por redes móviles/proxies.

### 7.4 Contraseñas, MFA y recuperación

- Preferir un proveedor OIDC existente cuando satisfaga los requisitos. Para identidad local, apoyarse en ASP.NET Core Identity y un hasher vigente; preferir Argon2id mantenido. Si existe requisito FIPS, usar PBKDF2-HMAC-SHA-256 con work factor acorde a la guía OWASP vigente.
- No implementar criptografía propia ni guardar contraseñas, refresh tokens o códigos MFA en texto claro. No usar hashes rápidos como SHA-256 para contraseñas.
- Como baseline sin MFA, exigir mínimo 15 caracteres; con MFA, nunca menos de 8. Admitir al menos 64 caracteres, Unicode y espacios, sin truncamiento silencioso ni reglas arbitrarias de composición.
- Bloquear contraseñas comunes/filtradas cuando el flujo de alta o cambio lo permita; no exigir cambio periódico sin evidencia de compromiso.
- MFA obligatorio para administradores; habilitar step-up MFA/reautenticación para cambios de seguridad, privilegios y acciones de impacto.
- Recuperación: respuesta genérica, token aleatorio no enumerable, almacenado como hash, de un solo uso, con vencimiento; tras cambio, invalidar sesiones según política.
- No permitir que el cambio de email, contraseña o MFA se complete únicamente por un access token de sesión larga; pedir reautenticación y/o MFA.

### 7.5 CSRF, CORS y cookies

- Como el navegador adjunta cookies automáticamente, proteger login, refresh, logout y cualquier operación autenticada por cookie con antiforgery. Verificar `Origin`; usar `Sec-Fetch-Site` como señal adicional.
- `SameSite` es defensa en profundidad, no sustituto de token CSRF. Nunca usar `SameSite=None` sin `Secure` y mitigaciones CSRF explícitas.
- CORS permite orígenes exactos y credenciales solo para los flujos que lo requieren. Nunca combinar `Access-Control-Allow-Credentials` con origen `*` ni confiar en subdominios wildcard.
- Recomendar publicar SPA y API bajo el mismo sitio/origen mediante reverse proxy. Si el despliegue requiere contextos cross-site, documentar restricciones del navegador y controles compensatorios antes de aprobarlo.
- El Bearer JWT es el mecanismo de autenticación de las rutas de negocio; la cookie refresh no autoriza automáticamente esas rutas.

## 8. Baseline de seguridad OWASP Top 10:2025

| Categoría | Requisitos de arquitectura y desarrollo |
|---|---|
| **A01 Broken Access Control** | Denegar por defecto; políticas por función; autorización por recurso/tenant en cada operación; DTOs allowlist; pruebas BOLA/IDOR y elevación de privilegios. |
| **A02 Security Misconfiguration** | Configuración segura por ambiente; CORS exacto; HTTPS/HSTS; cookies seguras; secretos en vault/secret manager; Swagger y errores detallados solo en desarrollo; proxies confiables explícitos. |
| **A03 Software Supply Chain Failures** | SDK y paquetes soportados; versiones centralizadas y lockfiles; revisión de vulnerabilidades transitivas; SBOM y análisis de dependencias en CI; imágenes base mantenidas. |
| **A04 Cryptographic Failures** | TLS moderno; hashing adaptativo; claves de firma separadas, protegidas y rotables; tokens mínimos; refresh almacenado como hash; no exponer secretos en logs/configuración. |
| **A05 Injection** | EF parametrizado; SQL crudo solo parametrizado y revisado; validación de límites; no formar comandos/consultas con entrada; escapar/serializar correctamente la salida. |
| **A06 Insecure Design** | Modelar amenazas y abuso de negocio antes de funciones sensibles; límites de operación, idempotencia, step-up auth y controles anti-automatización adecuados. |
| **A07 Authentication Failures** | Login y ciclo de sesión de esta sección; MFA para perfiles críticos; mensajes no enumerables; rate limiting; reset seguro; pruebas de rotación/revocación. |
| **A08 Software or Data Integrity Failures** | DTOs explícitos; validar webhooks con firma y protección replay; controlar deserialización; proteger pipeline, artefactos, migraciones y despliegues. |
| **A09 Security Logging and Alerting Failures** | Auditoría de login, MFA, cambios de privilegio y sesiones; métricas y alertas; correlación y retención definidas; nunca registrar contraseñas, tokens, cookies o secretos. |
| **A10 Mishandling of Exceptional Conditions** | Respuestas seguras y consistentes; `ProblemDetails`; no filtrar stack traces; fallar de forma cerrada; transacciones y cancelación correctas; no dejar cambios parciales. |

## 9. OWASP API Security Top 10:2023

| Riesgo API | Control de referencia |
|---|---|
| **API1 BOLA** | Comprobar acceso del principal al objeto, empresa/tenant y relación de negocio en cada endpoint que use IDs. |
| **API2 Broken Authentication** | Aplicar el flujo login, tokens, MFA, limitación y revocación descrito en la sección 7. |
| **API3 Broken Object Property Level Authorization** | DTOs separados por operación y allowlist de propiedades; no serializar entidades completas ni confiar en binding masivo. |
| **API4 Unrestricted Resource Consumption** | Límites por usuario/IP/operación, paginación acotada, tamaños máximos, timeouts, cancelación y cuotas a integraciones costosas. |
| **API5 Broken Function Level Authorization** | Políticas explícitas por endpoint/operación; pruebas de rol/privilegio para rutas administrativas y públicas. |
| **API6 Unrestricted Access to Sensitive Business Flows** | Rate/velocity limits, idempotencia y controles de negocio para flujos que generen costos, cambios o beneficios abusables. |
| **API7 SSRF** | Allowlist de destinos cuando proceda; validar esquema, host y resolución; bloquear redes internas no autorizadas; limitar redirects, tiempo y tamaño de respuesta. |
| **API8 Security Misconfiguration** | Hardening de ASP.NET, HTTPS, headers, CORS, configuración por ambiente y eliminación de endpoints de diagnóstico expuestos. |
| **API9 Improper Inventory Management** | Inventariar versiones, hosts, hubs y endpoints; mantener OpenAPI; retirar versiones obsoletas y verificar exposición en despliegue. |
| **API10 Unsafe Consumption of APIs** | Tratar respuestas externas como entrada no confiable; validar contenido, usar timeouts y límites; autenticar y observar fallos de integraciones. |

## 10. Validación, errores, secretos y logging

- Validar DTOs en la frontera HTTP con límites de tamaño, rango, formato y allowlists cuando aplique. La validación del cliente solo mejora UX.
- Devolver un formato estable `ProblemDetails`, con `traceId`; no incluir SQL, stack traces, tokens, claves, datos personales innecesarios ni detalles de infraestructura.
- Configuración sensible por variables de entorno/secret manager; validar al arranque. `.env.example` solo contiene nombres y valores ficticios.
- Restringir el usuario de base de datos al mínimo privilegio; separar credenciales de runtime, migraciones y administración.
- Logging estructurado y minimizado: registrar actor, acción, resultado, recurso necesario y correlación; nunca registrar passwords, access/refresh tokens, cookies, antiforgery tokens ni secretos.
- Confiar `X-Forwarded-For` y otros encabezados solo desde proxies/ingress declarados; no aceptar IP de cliente arbitraria como identidad confiable.
- En producción aplicar HTTPS/HSTS, `Cache-Control: no-store` en respuestas de autenticación y headers de seguridad apropiados a la aplicación.

## 11. Pruebas y controles de entrega

Estructurar pruebas por frontera, sin forzar repositorios solo para facilitar mocks:

- **Unitarias:** invariantes/reglas, autorización de aplicación, transiciones y validadores.
- **Integración:** EF Core/PostgreSQL, transacciones, restricciones, filtros por tenant y rotación de sesión.
- **API/end-to-end:** status codes, DTOs, headers/cookies, CORS/CSRF y flujos de autenticación.
- **Seguridad:** BOLA, permisos por función/propiedad, inyección, enumeración, rate limit, reset, MFA, refresh replay, logout y expiraciones.

Gates sugeridos en CI/CD: `dotnet restore`, `dotnet build -c Release`, `dotnet test`, análisis estático, escaneo de secretos, análisis de vulnerabilidades NuGet, construcción y escaneo de imagen, SBOM y DAST (por ejemplo OWASP ZAP) según el entorno. No liberar con hallazgos críticos/altos sin remediar o aceptar el riesgo de forma explícita, con responsable y vencimiento.

## 12. Docker y ejecución

- Construcción multi-stage con imagen `mcr.microsoft.com/dotnet/sdk:10.0` y runtime `mcr.microsoft.com/dotnet/aspnet:10.0`, actualizando parches regularmente.
- Publicar Release con `dotnet publish`; runtime sin SDK, sin herramientas de desarrollo y ejecutado como usuario no root.
- Puerto configurable mediante entorno; el backend web TMS usa el puerto interno `5000` como referencia, no como requisito funcional universal.
- PostgreSQL puede ir en Compose local independiente del API; en producción usar servicio gestionado o contenedor con volumen, red y credenciales separadas.
- `.dockerignore` excluye `.env`, secretos, `bin/`, `obj/`, resultados de pruebas y artefactos locales.
- No ejecutar migraciones destructivas automáticamente al iniciar producción sin procedimiento de despliegue y rollback definido.

## 13. Checklist para nuevas funcionalidades

1. Leer requisitos, solución y convenciones existentes antes de elegir estructura.
2. Definir actor, datos, permisos, abuso esperado y límites del flujo.
3. Crear únicamente endpoints, DTOs, servicios y entidades requeridos.
4. Validar input en API y reglas en Application/Domain.
5. Autorizar la función y el objeto/tenant en backend en todas las rutas afectadas.
6. Usar consultas parametrizadas y devolver DTOs allowlist.
7. Añadir pruebas unitarias y de integración/API proporcionales al riesgo.
8. Revisar secretos, logs, errores, límites de consumo e impacto en el Top 10/API Top 10.
9. Ejecutar build, pruebas y controles del pipeline afectados.
10. Documentar desviaciones de seguridad o arquitectura; no tomarlas silenciosamente.

## 14. Directrices para agentes de desarrollo

Al usar este documento como contexto:

1. Inspeccionar primero `.sln`, `.csproj`, `Program.cs`, configuraciones, módulos y pruebas existentes.
2. Tratar el stack y contratos explícitos como baseline. Si los requisitos chocan con ellos, describir el conflicto y pedir decisión.
3. Derivar módulos y reglas de requisitos reales; placeholders no son funcionalidades.
4. No añadir CRUDs, roles, claims, entidades, endpoints o capas sin requisito.
5. Proteger cada operación en backend; no asumir que una ruta React protegida autoriza una llamada.
6. Nunca almacenar ni imprimir secretos, credenciales, access tokens, refresh tokens o cookies.
7. Seguir el flujo de cookie/CSRF de la sección 7 cuando el cliente sea un navegador; no copiar el contrato de autenticación legacy del TMS.
8. No relajar un control OWASP para hacer pasar una prueba; investigar y documentar una excepción aprobada.
9. Ejecutar y reportar build, pruebas y análisis aplicables al cambio.
10. No introducir refactors ajenos a la funcionalidad solicitada.

## 15. Referencias de seguridad

- [OWASP Top 10:2025](https://owasp.org/Top10/2025/)
- [OWASP API Security Top 10:2023](https://owasp.org/API-Security/editions/2023/en/0x11-t10/)
- [OWASP ASVS 5.0.0](https://owasp.org/www-project-application-security-verification-standard/)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [OWASP Session Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html)
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [OWASP CSRF Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)
- [OWASP .NET Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/DotNet_Security_Cheat_Sheet.html)

Este estándar establece un baseline para proyectos nuevos, no define un dominio de negocio ni certifica por sí solo una aplicación. Los requisitos del producto determinan las funcionalidades; toda desviación de seguridad debe ser consciente, justificada y verificable.
