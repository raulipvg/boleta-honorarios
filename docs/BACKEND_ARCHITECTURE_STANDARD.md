# Arquitectura estándar del Backend

Este documento define el stack, la estructura estándar y las responsabilidades del backend. También establece el flujo de autenticación JWT Bearer y el contrato de sesión con el frontend.

## 1. Stack tecnológico

- **Runtime:** Node.js 24.
- **Framework API:** NestJS 12.1.1.
- **Lenguaje:** TypeScript.
- **Transporte:** API REST sobre HTTP/JSON, usando el adaptador Express predeterminado de NestJS.
- **Autenticación:** JWT Bearer para endpoints protegidos.
- **Persistencia:** PostgreSQL y Prisma.
- **Contenedores:** Docker y Docker Compose.

Las versiones de los paquetes de NestJS deben quedar alineadas con la versión acordada del framework. Las dependencias y versiones exactas se registran en `package.json` y `package-lock.json`.

## 2. Principios de arquitectura

- Organizar el backend por módulos funcionales.
- Mantener los controladores como adaptadores HTTP: reciben solicitudes, validan el contrato y delegan los casos de uso en servicios.
- Centralizar validaciones y comportamiento transversal sin mezclarlo con reglas de un módulo de negocio.
- No incluir secretos en el código ni exponer configuración privada al frontend.
- Validar autenticación y autorización en el backend, aunque el frontend limite la navegación.

## 3. Estructura del proyecto

```text
backend/
├── Dockerfile
├── compose.yaml
├── .dockerignore
├── .env.example
├── package.json
├── package-lock.json
├── nest-cli.json
├── tsconfig.json
├── prisma/
│   ├── schema.prisma
│   └── migrations/
└── src/
    ├── main.ts
    ├── app.module.ts
    ├── auth/
    │   ├── auth.module.ts
    │   ├── auth.controller.ts
    │   ├── auth.service.ts
    │   ├── dto/
    │   ├── decorators/
    │   ├── guards/
    │   └── strategies/
    ├── common/
    │   ├── filters/
    │   ├── interceptors/
    │   └── pipes/
    ├── config/
    ├── modules/
    │   └── <modulo>/
    │       ├── <modulo>.module.ts
    │       ├── <modulo>.controller.ts
    │       ├── <modulo>.service.ts
    │       └── dto/
    └── prisma/
        ├── prisma.module.ts
        └── prisma.service.ts
```

## 4. Responsabilidad de las áreas

- `main.ts`: configura el arranque HTTP, el prefijo global `/api`, CORS, validación global y demás configuración de la aplicación.
- `app.module.ts`: módulo raíz y composición de los módulos funcionales.
- `auth/`: login, emisión y renovación de tokens, estrategia JWT, guards y contratos de autenticación.
- `common/`: filtros, pipes, interceptores y utilidades transversales.
- `config/`: carga y validación de configuración proveniente del entorno.
- `modules/<modulo>/`: controllers, servicios, DTOs y casos de uso de cada funcionalidad.
- `src/prisma/`: módulo y servicio de integración con Prisma.
- `prisma/`: esquema de datos y migraciones.

La base PostgreSQL se ejecuta en un contenedor independiente del proceso NestJS. La configuración del contenedor de base de datos se mantiene separada de la estructura interna del backend.

## 5. Autenticación JWT Bearer

### 5.1. Política de tokens

- El backend emite un access token JWT de duración limitada.
- El frontend envía el access token en `Authorization: Bearer <accessToken>` para acceder a endpoints protegidos.
- El refresh token se entrega mediante una cookie `HttpOnly`, que no es accesible desde JavaScript.
- El refresh token se rota al renovar la sesión y puede revocarse al cerrar sesión.
- Las claves de firma, emisores, audiencias y otros secretos se configuran únicamente en el backend.
- La expiración concreta y los claims adicionales se definirán al implementar la autenticación.

### 5.2. Endpoints de referencia

| Método | Endpoint | Responsabilidad |
|---|---|---|
| `POST` | `/api/auth/login` | Valida credenciales, emite un access token y establece la cookie de refresh. |
| `POST` | `/api/auth/refresh` | Valida y rota la cookie de refresh; devuelve un nuevo access token. |
| `POST` | `/api/auth/logout` | Revoca la sesión de refresh y expira la cookie. |
| `GET` | `/api/auth/me` | Devuelve la identidad asociada al Bearer token actual. |

Los endpoints y respuestas describen el contrato inicial. Los DTOs y detalles definitivos se concretarán al implementar la autenticación.

### 5.3. Flujo de login

1. El frontend envía credenciales a `POST /api/auth/login`.
2. El controller valida el DTO y delega la operación en `AuthService`.
3. El servicio valida las credenciales y genera un access JWT si son correctas.
4. El backend devuelve el access token en la respuesta y establece la cookie de refresh con los atributos de seguridad configurados.
5. El frontend conserva el access token en memoria y lo usa en el encabezado Bearer.

El backend no debe incluir credenciales ni secretos en logs o mensajes de error.

### 5.4. Validación de requests protegidos

- Una estrategia JWT valida firma, expiración y, cuando se configure, emisor y audiencia.
- `JwtAuthGuard` protege endpoints que requieren identidad autenticada.
- Las rutas públicas, como login y refresh, se excluyen explícitamente del guard global o de ruta.
- Tras validar el token, el guard asocia la identidad autenticada al contexto de la solicitud.
- Los servicios usan esa identidad para aplicar las reglas funcionales correspondientes.
- La autorización por roles o permisos es una capa separada; no se considera resuelta solo con autenticación JWT.

### 5.5. Renovación de sesión

1. El frontend llama `POST /api/auth/refresh` con credenciales de cookie habilitadas.
2. El navegador envía la cookie `HttpOnly` automáticamente.
3. El backend valida la sesión de refresh, rota el token y devuelve un nuevo access JWT.
4. Si el refresh token es inválido, expiró o fue revocado, el endpoint responde `401` y elimina la cookie.

La estrategia de persistencia de refresh —por ejemplo, guardar una sesión revocable o el hash del token— debe permitir invalidarlo y evitar almacenar el token en claro.

### 5.6. Logout y expiración

- `POST /api/auth/logout` revoca la sesión de refresh y responde expirando la cookie.
- El frontend elimina su access token y estado de usuario al completar el logout.
- Como el access JWT es autocontenido, su validez residual termina con su expiración. Revocar el refresh impide emitir tokens posteriores.
- Una credencial ausente, inválida o expirada se responde con `401 Unauthorized`.
- Una identidad autenticada sin autorización para la operación se responde con `403 Forbidden`.

### 5.7. Cookies, CORS y protección CSRF

- La cookie de refresh usa `HttpOnly`; en producción también `Secure`.
- Configurar `SameSite=Lax` o `Strict` cuando el despliegue lo permita. Usar `SameSite=None; Secure` únicamente si frontend y API requieren contexto cross-site.
- CORS permite orígenes explícitos y credenciales; no debe combinar credenciales con un origen `*`.
- Validar el origen de las solicitudes que usan cookies y habilitar protección CSRF cuando corresponda al modelo de despliegue.
- Limitar `Path` y `Domain` de la cookie a lo necesario para las rutas de autenticación.

## 6. API, configuración y persistencia

- El prefijo inicial de los endpoints es `/api`.
- Validar los DTOs de entrada y mantener contratos de request/response explícitos.
- La configuración se carga desde variables de entorno y se valida durante el arranque.
- `.env.example` documenta nombres y formatos esperados, nunca valores secretos reales.
- `DATABASE_URL` y las claves JWT son configuración exclusiva del backend.
- Prisma centraliza el acceso a PostgreSQL; cambios al esquema se mantienen mediante migraciones.

## 7. Docker y ejecución

- `Dockerfile` define etapas de dependencias, build y ejecución de producción.
- `compose.yaml` proporciona el entorno de desarrollo con hot reload.
- NestJS escucha en `0.0.0.0:3000` dentro del contenedor y publica el puerto `3000`.
- El servicio backend se conecta a PostgreSQL como servicio externo al contenedor de NestJS.
- `.dockerignore` excluye dependencias locales, archivos `.env`, compilaciones y artefactos que no se requieren en la imagen.
- Las migraciones de Prisma se aplican mediante comandos de desarrollo o de despliegue según el entorno.

## 8. Flujo recomendado para agregar una funcionalidad

1. Crear el módulo en `src/modules/<modulo>/`.
2. Definir DTOs y el contrato REST.
3. Implementar la lógica en un servicio y mantener el controller enfocado en HTTP.
4. Aplicar validación de entrada y guards en los endpoints protegidos.
5. Implementar acceso a datos mediante Prisma cuando aplique.
6. Documentar las respuestas `401` y `403` esperadas para la funcionalidad.
