# Estándar de arquitectura: Frontend React + API .NET

Este documento define una referencia para frontends de nuevos proyectos y sirve como contexto técnico para desarrolladores y agentes de IA. Conserva React/Vite, como `TMS/tms-frontend`, e integra el contrato seguro descrito en `BACKEND_ARCHITECTURE_STANDARD.md` para ASP.NET Core.

Los nombres `<modulo>` y `<feature>` son placeholders, no funcionalidades que deban crearse automáticamente. Las decisiones de seguridad se alinean con OWASP Top 10:2025, OWASP API Security Top 10:2023 y OWASP ASVS 5.0.0; la matriz backend describe los controles del servidor y la API.

## 1. Stack tecnológico

- **Runtime de desarrollo/build:** Node.js 24 LTS como baseline; fijar versión por proyecto.
- **Framework:** React 19.1.0 y React DOM 19.1.0.
- **Lenguaje:** TypeScript 5.9.3.
- **Bundler y servidor de desarrollo:** Vite 8.3.2 con `@vitejs/plugin-react` 6.1.1.
- **Tipos React:** `@types/react` y `@types/react-dom` de la línea 19, compatibles con React 19.1.0.
- **Componentes UI:** Ant Design 6.6.5.
- **Routing:** React Router 7.13.0.
- **Cliente HTTP:** Axios 1.20.0 mediante un cliente HTTP centralizado y compartido por los servicios de dominio.
- **Fechas y horas:** Day.js 1.11.23 mediante utilidades compartidas.
- **Realtime opcional:** `@microsoft/signalr` 10.0.0 solo cuando existan requisitos de tiempo real.
- **Servidor estático y reverse proxy de producción:** Nginx `1.30.5-alpine`, fijado en la imagen Docker como `nginx:1.30.5-alpine`.
- **Contenedores:** Docker y Docker Compose; Vite se usa para desarrollo y Nginx para servir el build de producción.

Las versiones indicadas son el baseline acordado para nuevos proyectos. Node.js 24 satisface los requisitos de Vite 8. Fijar la versión Node (por ejemplo `.nvmrc`/`engines`), usar versiones compatibles entre sí y conservar `package-lock.json`. Las actualizaciones de dependencias se hacen deliberadamente y se revisan con análisis de vulnerabilidades.

## 2. Responsabilidades y límites de seguridad

- Presentar interfaz, navegación y estados de carga/error.
- Organizar pantallas, servicios y componentes por funcionalidades y dominios funcionales cohesivos.
- Consumir el backend ASP.NET Core mediante servicios tipados por dominio y un cliente HTTP compartido.
- Mantener el access token y el estado de identidad solo en memoria.
- Administrar UX de sesión, refresh, logout y rutas privadas sin pretender reemplazar autenticación ni autorización del backend.
- No decidir permisos de negocio de forma autoritativa en el cliente; el backend verifica cada operación y objeto.
- No incluir contraseñas, tokens, claves, credenciales, connection strings ni otros secretos en el bundle.

## 3. Estructura del proyecto

```text
frontend/
├── Dockerfile
├── nginx.conf
├── docker-compose.yaml
├── .dockerignore
├── .env.example
├── package.json
├── package-lock.json
├── index.html
├── vite.config.ts
├── tsconfig.json
└── src/
    ├── main.tsx
    ├── App.tsx
    ├── components/
    │   ├── layout/
    │   ├── authorization/
    │   │   └── Can.tsx
    │   └── ProtectedRoute.tsx
    ├── context/
    │   ├── AuthContext.tsx
    │   └── useAuth.ts
    ├── hooks/
    │   └── useAuthorization.ts
    ├── pages/
    │   ├── Login/
    │   └── <modulo>/
    ├── routes/
    ├── services/
    │   ├── apiClient.ts
    │   ├── auth/
    │   │   └── authService.ts
    │   ├── <dominio>/
    │   │   └── <dominio>Service.ts
    │   └── realtime/                    # opcional
    ├── types/
    │   └── authorization.ts
    ├── constants/
    ├── theme/
    └── utils/
        └── date.ts
```

Las pantallas y funcionalidades pueden agruparse por módulo cuando eso facilite mantenimiento. No es obligatorio crear cada carpeta de ejemplo ni mover código existente solo para ajustarlo a esta forma.

### Responsabilidad de carpetas y archivos

- `main.tsx`: monta React y providers globales.
- `App.tsx` y `routes/`: componen layout y declaran rutas.
- `components/`: componentes UI/layout reutilizables; mantenerlos pequeños y sin reglas de acceso de negocio.
- `pages/`: pantallas por módulo o funcionalidad.
- `context/AuthContext.tsx`: estado en memoria, identidad, carga inicial y operaciones de autenticación.
- `hooks/useAuthorization.ts` y `components/authorization/Can.tsx`: consultan permisos efectivos para rutas, menús y componentes sin duplicar reglas.
- `services/apiClient.ts`: configura el transporte HTTP compartido, base URL, Bearer token, CSRF y errores comunes.
- `services/auth/authService.ts`: contrato de autenticación del navegador, como login, restauración de sesión y logout.
- `services/<dominio>/<dominio>Service.ts`: expone operaciones HTTP tipadas de un dominio funcional cohesivo mediante el cliente compartido.
- `utils/date.ts`: concentra parseo, comparación, serialización y presentación de fechas con Day.js.
- `ProtectedRoute.tsx`: controla navegación por autenticación/roles/permisos como UX; no es una barrera de seguridad de API.
- `types/`, `constants/`, `hooks/` y `utils/`: elementos realmente compartidos y libres de secretos.

### Servicios organizados por dominio

- Un dominio frontend representa una capacidad funcional cohesiva de la aplicación. No tiene que corresponder uno a uno con un módulo, controller o proyecto del backend, ni implica adoptar Domain-Driven Design en el cliente.
- Agrupar en un mismo servicio las operaciones de API que pertenecen a esa capacidad. No crear un servicio único que mezcle dominios no relacionados, un servicio por pantalla ni un archivo por endpoint.
- Un servicio traduce operaciones tipadas del frontend al contrato HTTP: rutas relativas, parámetros, payloads y respuestas. Mantenerlo enfocado en acceso a API; no debe importar React ni contener estado de UI, presentación, autorización de negocio o reglas propias del producto.
- Dejar que los errores HTTP se propaguen al consumidor para que la funcionalidad decida cómo presentarlos; no ocultarlos ni convertirlos en éxitos desde el servicio.
- Los servicios de dominio usan `apiClient.ts`; no crean clientes Axios, interceptores ni políticas de sesión propios. La autenticación puede exponer sus operaciones en `services/auth/`, manteniendo el transporte y las políticas comunes en el cliente compartido.
- Una pantalla o un hook de funcionalidad puede coordinar llamadas de varios servicios. Como regla, un servicio de dominio no debe importar ni coordinar otros servicios de dominio. Si existe una orquestación reutilizable que no pertenece a la UI, identificarla explícitamente en vez de esconderla en un servicio genérico.
- Importar el servicio del dominio que se necesita. Evitar un servicio fachada global que vuelva a mezclar o reexportar operaciones de dominios distintos.

## 4. Organización de una funcionalidad

```text
src/pages/<modulo>/
├── index.tsx
├── components/
├── hooks/
│   └── <modulo>.types.ts
├── <modulo>.constants.ts      # opcional
└── utils.ts                   # opcional
```

La página compone la vista; los hooks encapsulan interacciones de UI; los servicios por dominio son el límite de comunicación con la API; los componentes son preferentemente presentacionales. Una página o hook puede coordinar varios servicios para una funcionalidad sin trasladar esa composición a un servicio global. No introducir una capa global o abstracción nueva hasta observar una necesidad real.

## 5. Routing, estado y API

- `/login` y los flujos de recuperación/MFA son rutas públicas cuando estén requeridos.
- Las rutas privadas usan `ProtectedRoute` y distinguen sesión en carga, no autenticada, autenticada y autenticada sin permiso.
- La autorización visual usa roles/permisos entregados por `/api/auth/me`; evitar checks de rol duplicados en cada página y componente.
- Los permisos de menús, rutas y botones son indicativos. Ante `403`, mostrar denegación apropiada; no asumir que ocultar un botón protege el endpoint.
- Por defecto el navegador consume la API en el mismo origen mediante `/api`; Nginx reenvía ese path al backend .NET.
- `API_PROXY_URL` configura en tiempo de ejecución el upstream interno de Nginx, por ejemplo `http://api:5000`; no es un secreto y no se incluye en el bundle.
- `VITE_API_URL` solo se define cuando un despliegue necesita una URL pública directa distinta; nunca contiene secretos ni credenciales.
- `apiClient` normaliza el prefijo `/api` en un solo sitio. Los servicios por dominio usan rutas relativas y no repiten ese prefijo.
- Durante desarrollo, Vite puede proxyar `/api` al backend para conservar una URL de navegador equivalente a producción.
- El cliente centralizado aplica timeout razonable, `Authorization: Bearer` solo cuando hay access token en memoria, CSRF header cuando corresponda y mapeo estable de errores.
- Al servir SPA y API en el mismo origen mediante Nginx, no se requiere CORS para ese flujo. Si hay acceso cross-origin explícito, el backend permite solo orígenes exactos y credenciales necesarias; nunca wildcard con credenciales.
- Usar HTTPS en desarrollo y producción para probar cookies seguras y evitar diferencias entre ambientes.

### 5.1 Fechas y zonas horarias

- Usar Day.js 1.11.23 y centralizar funciones compartidas en `src/utils/date.ts`; evitar parseo/formateo duplicado en pantallas y componentes.
- Mantener operaciones de calendario con objetos Day.js y serializar en el límite del servicio/API. No depender del timezone local del navegador para reglas de negocio.
- Intercambiar instantes con el backend como ISO 8601 con zona horaria explícita; usar UTC (`Z`) como formato estándar de salida salvo que el contrato defina otra zona.
- Tratar fechas sin hora (por ejemplo, cumpleaños o día de vencimiento) como valores civiles `YYYY-MM-DD`. No convertirlas a instantes ni pasarlas por `Date.toISOString()`, porque una conversión de zona podría cambiar el día.
- Presentar fechas con el locale de la interfaz y la zona horaria definida por el producto/usuario. No inferir la zona de negocio desde la configuración del dispositivo.
- Cuando los requisitos necesiten conversión entre zonas IANA, configurar los plugins oficiales UTC y Timezone de Day.js; cargar solo los locales requeridos y mantener explícita la zona usada en cada operación.
- Validar valores de entrada y tratar fechas inválidas explícitamente; no aceptar parseos ambiguos o formatos dependientes del navegador.
- Usar Day.js para los valores de fecha de Ant Design DatePicker y convertirlos a los formatos del contrato en el servicio correspondiente.

## 6. Autenticación segura del frontend

### 6.1 Política de almacenamiento

- **Access token JWT:** duración de 10 minutos; solo en memoria (por ejemplo, AuthContext); no guardar en `localStorage`, `sessionStorage`, IndexedDB, cookies legibles por JS ni URL.
- **Refresh token:** secreto opaco, no JWT, exclusivamente en cookie `HttpOnly` emitida por el backend; JavaScript no lo lee ni lo incluye en JSON. El servidor aplica 8 horas de inactividad y 7 días absolutos.
- **Identidad:** mantener en memoria. Si se guarda preferencia visual, no incluir token, información sensible ni datos que permitan restaurar una sesión.
- La cookie usa `Secure`, `HttpOnly`, host-only, `Path=/`, `SameSite=Strict` o `Lax` según el flujo y un nombre `__Host-...`. Los atributos los fija el servidor .NET.
- Cifrar un token de browser-storage con una clave disponible al mismo JavaScript no equivale a protegerlo de XSS; por eso el estándar no permite persistir credenciales en web storage.

### 6.2 Flujo de login

1. Al cargar la app, `AuthContext` comienza en estado `loading`, sin mostrar pantallas privadas.
2. `authService` llama `GET /api/auth/csrf` y conserva el token antifalsificación en memoria.
3. El usuario envía identificador y contraseña con `POST /api/auth/login`, `X-CSRF-TOKEN` y credenciales de navegador habilitadas según configuración CORS.
4. La respuesta de credenciales inválidas siempre se muestra de forma genérica; el cliente no presenta mensajes distintos para cuenta inexistente, contraseña errónea o cuenta deshabilitada.
5. Si el backend requiere MFA, mostrar el desafío correspondiente y enviar el código con `POST /api/auth/mfa/verify`. No marcar al usuario autenticado antes de que el backend confirme la autenticación completa.
6. En éxito, el backend retorna solo un access token de corta duración en JSON y establece el refresh cookie HttpOnly. El frontend guarda el access token únicamente en memoria.
7. `AuthContext` marca al usuario autenticado y llama `GET /api/auth/me` para obtener la identidad mínima necesaria para la UI.

La respuesta de `/api/auth/me` incluye `roleCodes` y `permissionCodes` efectivos. El frontend los guarda solo en memoria; son un snapshot para decidir qué mostrar y no sustituyen autorización del backend.

### 6.3 Inicio/restauración de sesión

1. Tras recargar, el frontend no intenta reconstruir una sesión leyendo tokens de storage.
2. Obtiene/renueva el token CSRF y solicita `POST /api/auth/refresh` con cookie automática.
3. Si el backend acepta y rota el refresh token, responde con nuevo access token y vuelve a establecer cookie. El front lo mantiene en memoria y carga `/api/auth/me`.
4. Si refresh devuelve `401`, expira o falla, limpiar identidad y token en memoria y dirigir al login.
5. La app no debe mostrar contenido privado durante la verificación inicial ni confiar en un valor persistido de `isAuthenticated`.

### 6.4 Interceptor HTTP y concurrencia

- El interceptor de solicitud agrega `Authorization: Bearer <accessToken>` cuando la solicitud lo necesita.
- `withCredentials` se habilita en endpoints que usan refresh cookie/CSRF; los endpoints de negocio usan Bearer y el servidor no debe aceptar la cookie refresh como autorización general.
- Ante `401`, intentar refresh como máximo una vez y reintentar la solicitud original una sola vez. Excluir login, refresh, CSRF y endpoints que no deben renovar.
- Las solicitudes concurrentes comparten una única renovación en la pestaña; coordinar pestañas cuando sea posible para evitar carreras al rotar cookies.
- Si la renovación falla, limpiar el estado local en memoria, invalidar caches autenticadas y navegar a login.
- Un `403` no dispara refresh. Puede volver a sincronizar permisos visibles, pero el servidor conserva autoridad final.
- Evitar bucles de refresh, retries ilimitados y retries automáticos de operaciones no idempotentes.

### 6.5 Logout y cambios de seguridad

1. Llamar `POST /api/auth/logout` con credenciales de cookie y antiforgery.
2. El backend revoca la sesión y expira la cookie; el frontend limpia access token, identidad y caches asociados.
3. Si el endpoint falla, limpiar de todos modos el estado local y explicar que el cierre remoto no pudo confirmarse según la UX acordada.
4. Para “cerrar todas las sesiones”, cambio de contraseña, recuperación o cambio de MFA, llamar la operación explícita de backend y descartar access token/local state.

### 6.6 Autorización RBAC en rutas y componentes

Los roles base de este sistema son `ADMINISTRADOR` y `PROFESIONAL`. Una cuenta puede tener varios roles. Mantener códigos estables en la lógica y etiquetas localizadas aparte. La matriz de capacidades de cada rol aún debe definirse con requisitos; el frontend no asigna permisos por su cuenta.

- `AuthContext` conserva `roleCodes: string[]` y los códigos de `permissionCodes` recibidos de `/api/auth/me`, junto con un `Set` en memoria para consultas. Tras reload, vuelve a obtenerlos del backend mediante el flujo de sesión.
- `ProtectedRoute` permite declarar `requiredRoles` y/o `requiredPermissions`. Mientras la sesión carga, muestra carga; si no hay sesión, redirige a login conservando la ruta de retorno; si la sesión existe pero no cumple el requisito, muestra/dirige a una pantalla `403`.
- Cuando `requiredRoles` contiene varios roles, el modo `any` es el predeterminado (basta uno); usar `all` solo si la regla lo requiere. `requiredPermissions` requiere todos (`all`) por defecto; usar `any` únicamente cuando el requisito lo indique explícitamente.
- Preferir guards por permiso para páginas/operaciones concretas. El check por rol queda para restricciones gruesas de área o navegación y no reemplaza el permiso de acción.
- `useAuthorization()` expone consultas centralizadas como `hasRole`, `hasPermission` y `hasAllPermissions`. `<Can permission="<modulo>:<recurso>:<accion>" />` permite ocultar o renderizar contenido alternativo para controles concretos.
- Los botones, links, menús, acciones de tabla y secciones sensibles se muestran solo cuando la UI snapshot incluye el permiso requerido. Se puede deshabilitar en vez de ocultar cuando exista una razón UX explícita, sin tratarlo como control de seguridad.
- No distribuir condicionales del tipo `user.role === 'ADMINISTRADOR'` por la aplicación. No codificar permisos no acordados ni inferir que Administrador tiene acceso irrestricto a todos los datos.
- Si el backend devuelve `403`, no reintentar refresh; opcionalmente sincronizar `/api/auth/me` una sola vez si el producto necesita reflejar una revocación de permisos.
- Cambiar el rol o permiso en el cliente nunca otorga acceso: API .NET aplica RBAC y autorización por recurso en cada operación.

### 6.7 Política de tokens en el cliente

- El backend emite access JWT con expiración de 10 minutos. El frontend lo mantiene en memoria y lo envía solo en `Authorization: Bearer`; no emite, firma ni valida su propia autorización a partir del token.
- El cliente puede leer `exp` únicamente para UX (por ejemplo, evitar enviar una solicitud con un token evidentemente vencido); la aceptación, claims, firma y autorización se deciden siempre en .NET.
- El refresh es un secreto opaco en cookie `HttpOnly`, con máximo de 8 horas de inactividad y 7 días absolutos. El frontend no lo recibe en respuestas JSON ni intenta renovarlo en segundo plano sin actividad del usuario.
- La sesión se restaura al iniciar la aplicación llamando a `/api/auth/refresh`. Durante el uso, `401` puede disparar una renovación compartida y un único reintento; no renovar ante `403` ni mantener sesiones activas con polling de refresh.
- `POST /api/auth/refresh` rota la cookie y solo devuelve un access token nuevo; el cliente no envía el refresh token en body, query ni headers propios.
- No transportar access JWT en query string. La excepción técnica de SignalR queda limitada a los hubs y reglas de logging descritos en la sección 9.
- Logout limpia el access token de memoria; el backend revoca la sesión refresh. Los fallos no autorizan al frontend a conservar localmente una sesión como si siguiera activa.

## 7. CSRF, XSS y protección del navegador

- Incluir el token antifalsificación en encabezado (`X-CSRF-TOKEN`) para login, refresh, logout y toda operación que dependa de cookies.
- No confiar solo en `SameSite` ni en CORS como protección CSRF; el servidor valida token/origen. No realizar cambios de estado con `GET`.
- Respetar el escape seguro de React. No usar `dangerouslySetInnerHTML`; si un requisito lo exige, sanitizar con librería mantenida y validar el flujo con revisión de seguridad.
- Evitar scripts de terceros innecesarios; documentar los que se admitan. Configurar CSP y otros headers de seguridad en el servidor/reverse proxy, considerando los requisitos reales de React/Ant Design; no debilitar CSP globalmente por comodidad.
- No incluir secretos en código, sourcemaps públicos, bundle, variables `VITE_*`, mensajes de error, consola, analítica o telemetría.
- Tratar todo dato de API, URL, query string, archivo importado o almacenamiento local como entrada no confiable.
- Limpiar estado sensible/caches ante logout, expiración, cambio de usuario y cambio de tenant.

## 8. OWASP Top 10:2025 en el frontend

| Categoría | Requisitos frontend |
|---|---|
| **A01 Broken Access Control** | `ProtectedRoute` y `<Can>` controlan UX; no sustituyen autorización RBAC por función y recurso en backend. No inferir permisos ni alcance por IDs o valores del browser. |
| **A02 Security Misconfiguration** | No incluir secretos; usar HTTPS; configurar CSP/headers en hosting; no exponer configuración dev ni sourcemaps sensibles; verificar CORS con backend. |
| **A03 Software Supply Chain Failures** | Mantener `package-lock.json`; revisar dependencias y scripts; escanear npm dependencies; limitar paquetes y scripts de build. |
| **A04 Cryptographic Failures** | No persistir credenciales en web storage; no implementar criptografía propia; usar HTTPS para sesión completa. |
| **A05 Injection** | Mantener escape React; evitar HTML no confiable, construcción de URLs ejecutables y código dinámico; validar archivos/datos antes de presentar. |
| **A06 Insecure Design** | Diseñar flujos de errores, logout, MFA, carga y permisos; considerar abuso de acciones sensibles y navegación directa a rutas. |
| **A07 Authentication Failures** | Implementar solo el flujo de memoria + cookie HttpOnly documentado; no gestionar contraseña/token de refresh en el cliente. |
| **A08 Software or Data Integrity Failures** | Validar forma de respuestas y archivos externos; no ejecutar/deserializar contenido no confiable como código. |
| **A09 Security Logging and Alerting Failures** | No escribir passwords, tokens, cookies, datos personales o headers de autenticación en consola/telemetría. |
| **A10 Mishandling of Exceptional Conditions** | Mostrar errores genéricos y accionables, sin stack trace; distinguir UX de error HTTP sin filtrar detalles internos. |

Los controles de OWASP API Security Top 10 (autorización por objeto/campo/función, limitación de recursos, SSRF e inventario de endpoints, entre otros) se aplican en el backend y no se consideran resueltos por React.

## 9. Tiempo real con SignalR (opcional)

- Usar SignalR solo si un requisito necesita actualizaciones en tiempo real.
- El cliente obtiene el access token en memoria mediante `accessTokenFactory`; no persiste un token separado para SignalR.
- El backend limita el token a los hubs requeridos, verifica permisos para grupos/recursos y no autoriza por el nombre de grupo enviado por el cliente.
- Algunas negociaciones WebSocket pueden transportar `access_token` en query string por restricciones del navegador; exigir HTTPS, aceptar ese mecanismo solo en las rutas de hub necesarias y configurar proxy/APM/logging para redactar ese parámetro.
- Gestionar expiración/reconexión sin reusar refresh tokens desde el cliente y evitar reintentos agresivos.

## 10. Docker, build y configuración

- **Producción usa Nginx como baseline.** El `Dockerfile` multi-stage usa Node.js 24 para instalar dependencias y compilar React; la etapa final parte de `nginx:1.30.5-alpine`, copia `dist/` y la configuración Nginx, y no incluye Node/Vite en runtime.
- Nginx escucha en el puerto `8080`; Docker Compose publica el puerto elegido por el despliegue (la referencia TMS usa `8080:8080`).
- `nginx.conf` define el servidor estático y el reverse proxy:
  - `location /` entrega los archivos y utiliza fallback a `/index.html` para rutas de React Router.
  - `location /api/` reenvía solicitudes a `API_PROXY_URL`, configurado con DNS/puerto interno del backend .NET; conserva headers necesarios de autenticación, antiforgery y proxy.
  - Si se usa SignalR, habilita `Upgrade`/`Connection` para WebSocket en el upstream correspondiente y define timeouts razonables.
- El proxy de API no debe cachear respuestas autenticadas. Las respuestas de autenticación y contenido privado usan `Cache-Control: no-store`.
- Los assets con hash pueden usar caché larga e `immutable`; `index.html` se sirve sin caché prolongada para que los despliegues nuevos entren en vigor.
- Configurar compresión y headers de seguridad acordes con la aplicación, incluyendo `X-Content-Type-Options`, `Referrer-Policy`, protección contra framing y CSP. HSTS se añade en el punto que termina HTTPS: Nginx si termina TLS o el ingress/load balancer si termina TLS antes del contenedor.
- Los límites de solicitudes de Nginx pueden servir como defensa de borde y deben ajustarse a los flujos reales; no reemplazan el rate limiting y la autorización en ASP.NET.
- El contenedor de producción se ejecuta con privilegios mínimos y capacidades reducidas cuando la imagen/configuración lo permita; los directorios temporales necesarios se declaran explícitamente si el filesystem es de solo lectura.
- `docker-compose.yaml` mantiene desarrollo con hot reload en perfil de desarrollo; Vite escucha en `0.0.0.0` y publica el puerto de desarrollo acordado (la referencia TMS usa `5173`). El servicio productivo se valida con el build estático servido por Nginx.
- `.dockerignore` excluye `node_modules`, `dist`, `.env`, logs y artefactos locales.
- `.env.example` documenta `API_PROXY_URL` y configuración pública no sensible; `.env` local no se versiona. Nunca introducir secretos en `VITE_*` porque forman parte del bundle.

## 11. Pruebas y controles de entrega

- **Unitarias:** servicios/hooks de UI, estado de sesión, validación y manejo de errores.
- **Fechas:** parseo y serialización, cambio de día por zona horaria, límites de día, cambios de horario estacional, fecha bisiesta, locale y valores inválidos.
- **Integración:** Axios/authService con API mockeada de forma realista, CSRF, refresh concurrente, respuesta con `Cache-Control: no-store` y flujo de expiración.
- **E2E:** login, MFA si aplica, restauración tras reload, expiración del access token de 10 minutos, sesión idle/absoluta, guards de ruta y componentes para Administrador, Profesional, roles múltiples y usuario sin permisos, `401`, `403` y logout.
- **Seguridad:** comprobar que tokens no aparecen en local/session storage, URLs, logs o errores; verificar cookie HttpOnly en integración con backend; probar XSS/CSRF y manejo de contenido externo. Intentar llamadas directas a API sin permisos para confirmar que el backend responde `403` aunque el componente esté oculto.
- **Contenedor/proxy:** construir la imagen de producción; ejecutar `nginx -t`; verificar fallback de rutas, proxy `/api/`, headers, caché, tamaño/timeouts y WebSocket si SignalR está habilitado.
- Pipeline recomendado: `npm ci`, `npm run lint`, `npm run build`, auditoría de dependencias, secret scanning, análisis estático y DAST según el entorno.
- No aprobar vulnerabilidades críticas/altas sin corregirlas o registrar aceptación explícita del riesgo, responsable y vencimiento.

## 12. Checklist para nuevas funcionalidades

1. Leer estructura, dependencias, rutas y convenciones antes de modificar.
2. Derivar páginas, formularios, servicios por dominio y tipos de requisitos reales.
3. Antes de añadir una llamada HTTP, identificar el dominio propietario y ampliar su servicio existente si corresponde; no crear un servicio catch-all, uno por pantalla ni uno por endpoint.
4. Consumir API mediante los servicios de dominio y el cliente HTTP compartido; coordinar varios dominios desde la funcionalidad que los usa.
5. No guardar tokens ni asumir que `ProtectedRoute` protege backend.
6. Declarar los permisos de ruta y componente a partir de la matriz aprobada; no inferirlos del nombre del rol.
7. Enviar antiforgery donde el contrato cookie lo requiera y controlar orígenes.
8. Evitar contenido HTML no confiable, secretos y datos sensibles en logs.
9. Manejar carga, `401`, `403` y errores sin bucles de retry.
10. Probar build, lint y permisos UI; mantener lockfile y revisar dependencias introducidas.
11. No crear páginas o módulos de ejemplo no solicitados ni realizar reorganizaciones ajenas.

## 13. Directrices para agentes de desarrollo

Al utilizar este documento como contexto:

1. Inspeccionar primero el proyecto y respetar sus convenciones y dependencias bloqueadas.
2. Tratar el stack y los contratos de backend definidos como baseline; reportar conflictos en lugar de cambiar contratos silenciosamente.
3. Usar los roles base `ADMINISTRADOR` y `PROFESIONAL`; una cuenta puede tener varios. No deducir permisos o funcionalidades de placeholders.
4. Mantener autenticación centralizada en `AuthContext` y servicios; nunca copiar el mecanismo de `localStorage` del TMS actual.
5. Consultar `roleCodes`/`permissionCodes` con guards/hooks/componentes centralizados; no crear reglas de permiso ad hoc.
6. Mantener secretos fuera del bundle y de toda variable `VITE_*`.
7. No afirmar que un guard, permiso de componente o menú oculto reemplaza autorización de backend.
8. Ejecutar y reportar las pruebas/build afectados sin refactors no solicitados.
9. Al añadir o mover una llamada HTTP, respetar el límite del dominio funcional existente, usar `apiClient` y revisar consumidores antes de crear otro servicio o una fachada global.

Este estándar describe un baseline para nuevos frontends React conectados a ASP.NET Core. Para este sistema fija los roles `ADMINISTRADOR` y `PROFESIONAL`, pero la matriz de permisos, módulos y rutas de negocio solo se deriva de requisitos explícitos.

## 14. Referencias

- [OWASP Top 10:2025](https://owasp.org/Top10/2025/)
- [OWASP API Security Top 10:2023](https://owasp.org/API-Security/editions/2023/en/0x11-t10/)
- [OWASP ASVS 5.0.0](https://owasp.org/www-project-application-security-verification-standard/)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [OWASP Session Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html)
- [OWASP CSRF Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)
- [OWASP Web Frontend Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Web_Frontend_Security_Cheat_Sheet.html)
