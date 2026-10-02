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
- **Cliente HTTP:** Axios 1.20.0 mediante cliente(s) centralizados.
- **Realtime opcional:** `@microsoft/signalr` 10.0.0 solo cuando existan requisitos de tiempo real.
- **Contenedores:** Docker y Docker Compose según necesidades de desarrollo/despliegue.

Las versiones indicadas son el baseline acordado para nuevos proyectos. Node.js 24 satisface los requisitos de Vite 8. Fijar la versión Node (por ejemplo `.nvmrc`/`engines`), usar versiones compatibles entre sí y conservar `package-lock.json`. Las actualizaciones de dependencias se hacen deliberadamente y se revisan con análisis de vulnerabilidades.

## 2. Responsabilidades y límites de seguridad

- Presentar interfaz, navegación y estados de carga/error.
- Organizar pantallas, servicios y componentes por funcionalidades requeridas.
- Consumir el backend ASP.NET Core mediante servicios tipados y un cliente HTTP centralizado.
- Mantener el access token y el estado de identidad solo en memoria.
- Administrar UX de sesión, refresh, logout y rutas privadas sin pretender reemplazar autenticación ni autorización del backend.
- No decidir permisos de negocio de forma autoritativa en el cliente; el backend verifica cada operación y objeto.
- No incluir contraseñas, tokens, claves, credenciales, connection strings ni otros secretos en el bundle.

## 3. Estructura del proyecto

```text
frontend/
├── Dockerfile
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
    │   └── ProtectedRoute.tsx
    ├── context/
    │   ├── AuthContext.tsx
    │   └── useAuth.ts
    ├── hooks/
    ├── pages/
    │   ├── Login/
    │   └── <modulo>/
    ├── routes/
    ├── services/
    │   ├── apiClient.ts
    │   ├── authService.ts
    │   └── realtime/                    # opcional
    ├── types/
    ├── constants/
    ├── theme/
    └── utils/
```

Las pantallas y funcionalidades pueden agruparse por módulo cuando eso facilite mantenimiento. No es obligatorio crear cada carpeta de ejemplo ni mover código existente solo para ajustarlo a esta forma.

### Responsabilidad de carpetas y archivos

- `main.tsx`: monta React y providers globales.
- `App.tsx` y `routes/`: componen layout y declaran rutas.
- `components/`: componentes UI/layout reutilizables; mantenerlos pequeños y sin reglas de acceso de negocio.
- `pages/`: pantallas por módulo o funcionalidad.
- `context/AuthContext.tsx`: estado en memoria, identidad, carga inicial y operaciones de autenticación.
- `services/apiClient.ts`: configura Axios, base URL, Bearer token, CSRF y errores HTTP comunes.
- `services/authService.ts`: contrato de login, refresh, logout, CSRF y consulta de identidad.
- `ProtectedRoute.tsx`: evita navegación visual a pantallas privadas; no es una barrera de seguridad de API.
- `types/`, `constants/`, `hooks/` y `utils/`: elementos realmente compartidos y libres de secretos.

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

La página compone la vista; los hooks encapsulan interacciones de UI; los servicios son el límite de comunicación con la API; los componentes son preferentemente presentacionales. No introducir una capa global o abstracción nueva hasta observar una necesidad real.

## 5. Routing, estado y API

- `/login` y los flujos de recuperación/MFA son rutas públicas cuando estén requeridos.
- Las rutas privadas usan `ProtectedRoute` para UX, considerando estados `loading`, `authenticated` y `unauthenticated`.
- Los permisos que se usen para menús o botones son indicativos. Ante `403`, mostrar denegación apropiada; no asumir que ocultar un botón protege el endpoint.
- `VITE_API_URL` contiene una URL pública de API, por ejemplo `https://localhost:5001`; no contiene secretos ni credenciales.
- El servicio normaliza el prefijo `/api` en un solo sitio. No duplicar prefijos entre `.env` y cada endpoint.
- El cliente centralizado aplica timeout razonable, `Authorization: Bearer` solo cuando hay access token en memoria, CSRF header cuando corresponda y mapeo estable de errores.
- El backend habilita CORS para orígenes específicos y credenciales solo para los endpoints de autenticación con cookies. No permitir wildcard con credenciales.
- Usar HTTPS en desarrollo y producción para probar cookies seguras y evitar diferencias entre ambientes.

## 6. Autenticación segura del frontend

### 6.1 Política de almacenamiento

- **Access token JWT:** solo en memoria (por ejemplo, AuthContext); no guardar en `localStorage`, `sessionStorage`, IndexedDB, cookies legibles por JS ni URL.
- **Refresh token:** exclusivamente en cookie `HttpOnly` emitida por el backend; JavaScript no lo lee ni lo incluye en JSON.
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
| **A01 Broken Access Control** | Las rutas protegidas son UX; ocultar controles no sustituye autorización backend. No inferir acceso por IDs o valores del browser. |
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

- `Dockerfile` multi-stage para build y servicio web estático en producción (por ejemplo Nginx u otro servidor configurado para SPA); no ejecutar el servidor Vite de desarrollo en producción.
- `docker-compose.yaml` ofrece desarrollo local con hot reload solo en perfil de desarrollo.
- Vite escucha en `0.0.0.0` dentro del contenedor y publica el puerto de desarrollo acordado (la referencia TMS usa 5173).
- `.dockerignore` excluye `node_modules`, `dist`, `.env`, logs y artefactos locales.
- `.env.example` documenta URL pública y configuración no sensible; `.env` local no se versiona.
- El servidor de estáticos configura fallback de rutas React, HTTPS en despliegue y headers como CSP, HSTS, `X-Content-Type-Options` y `Referrer-Policy` de acuerdo con el hosting.
- Configurar `Cache-Control: no-store` para respuestas de autenticación y evitar cache/CDN de contenido privado.

## 11. Pruebas y controles de entrega

- **Unitarias:** servicios/hooks de UI, estado de sesión, validación y manejo de errores.
- **Integración:** Axios/authService con API mockeada de forma realista, CSRF, refresh concurrente y flujo de expiración.
- **E2E:** login, MFA si aplica, restauración tras reload, rutas protegidas, `401`, `403`, logout y vencimiento.
- **Seguridad:** comprobar que tokens no aparecen en local/session storage, URLs, logs o errores; verificar cookie HttpOnly en integración con backend; probar XSS/CSRF y manejo de contenido externo.
- Pipeline recomendado: `npm ci`, `npm run lint`, `npm run build`, auditoría de dependencias, secret scanning, análisis estático y DAST según el entorno.
- No aprobar vulnerabilidades críticas/altas sin corregirlas o registrar aceptación explícita del riesgo, responsable y vencimiento.

## 12. Checklist para nuevas funcionalidades

1. Leer estructura, dependencias, rutas y convenciones antes de modificar.
2. Derivar páginas, formularios, servicios y tipos de requisitos reales.
3. Consumir API solo mediante cliente/servicios centralizados.
4. No guardar tokens ni asumir que `ProtectedRoute` protege backend.
5. Enviar antiforgery donde el contrato cookie lo requiera y controlar orígenes.
6. Evitar contenido HTML no confiable, secretos y datos sensibles en logs.
7. Manejar carga, `401`, `403` y errores sin bucles de retry.
8. Probar build, lint y comportamiento de seguridad afectado.
9. Mantener lockfile y revisar dependencias introducidas.
10. No crear páginas o módulos de ejemplo no solicitados ni realizar reorganizaciones ajenas.

## 13. Directrices para agentes de desarrollo

Al utilizar este documento como contexto:

1. Inspeccionar primero el proyecto y respetar sus convenciones y dependencias bloqueadas.
2. Tratar el stack y los contratos de backend definidos como baseline; reportar conflictos en lugar de cambiar contratos silenciosamente.
3. No deducir funcionalidades de placeholders.
4. Mantener autenticación centralizada en `AuthContext` y servicios; nunca copiar el mecanismo de `localStorage` del TMS actual.
5. Mantener secretos fuera del bundle y de toda variable `VITE_*`.
6. No afirmar que un control de frontend reemplaza autorización de backend.
7. Ejecutar y reportar las pruebas/build afectados sin refactors no solicitados.

Este estándar describe un baseline para nuevos frontends React conectados a ASP.NET Core. El dominio del producto, los roles y las rutas de negocio solo se derivan de requisitos explícitos.

## 14. Referencias

- [OWASP Top 10:2025](https://owasp.org/Top10/2025/)
- [OWASP API Security Top 10:2023](https://owasp.org/API-Security/editions/2023/en/0x11-t10/)
- [OWASP ASVS 5.0.0](https://owasp.org/www-project-application-security-verification-standard/)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [OWASP Session Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html)
- [OWASP CSRF Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)
- [OWASP Web Frontend Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Web_Frontend_Security_Cheat_Sheet.html)
