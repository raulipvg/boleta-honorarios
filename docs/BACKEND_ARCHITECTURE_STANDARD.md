# Propuesta técnica base: Backend para MVP

Este documento sirve como contexto técnico para construir backends de distintos productos con el stack acordado. Propone un monolito modular NestJS con pocas capas explícitas, adecuado para comenzar un MVP sin anticipar dominios ni reglas de negocio que todavía no se han definido.

Los nombres `<modulo>`, `<entidad>` y `<operacion>` son placeholders estructurales. No representan funcionalidades que deban crearse automáticamente.

## 1. Stack tecnológico

- **Runtime:** Node.js 24.
- **Framework API:** NestJS 12.1.1.
- **Lenguaje:** TypeScript.
- **Transporte HTTP:** API REST sobre HTTP/JSON, usando el adaptador Express predeterminado de NestJS.
- **Persistencia:** PostgreSQL y Prisma.
- **Autenticación:** JWT Bearer para endpoints protegidos y refresh token mediante cookie `HttpOnly`.
- **Contenedores:** Docker y Docker Compose.

Este stack es la base tecnológica fija de la propuesta. Las versiones acordadas se registran en `package.json` y `package-lock.json`; no se cambian sin una decisión explícita del proyecto. PostgreSQL se ejecuta en un contenedor independiente del backend.

## 2. Principios para el MVP

- Mantener un único backend modular organizado por funcionalidades.
- Crear módulos solo cuando los requisitos definan sus responsabilidades.
- Usar `controller + service + DTOs` como estructura normal de cada módulo.
- Permitir que los servicios inyecten `PrismaService` directamente para las operaciones iniciales.
- Mantener el controller enfocado en HTTP y delegar la lógica de funcionalidad al service.
- Usar validación en los límites HTTP y guards en las operaciones protegidas.
- Evitar repositorios genéricos, puertos, capas de dominio y casos de uso independientes cuando no aporten valor concreto.
- Mantener secretos y configuración sensible fuera del código y del frontend.

Esta propuesta prioriza simplicidad y separación por módulos. No describe una arquitectura hexagonal completa: el acceso directo del service a Prisma se acepta como una decisión pragmática para el MVP.

## 3. Estructura de carpetas

```text
backend/
├── Dockerfile
├── docker-compose.yaml
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
    ├── common/
    │   ├── filters/
    │   ├── interceptors/
    │   ├── pipes/
    │   └── decorators/
    ├── config/
    ├── database/
    │   ├── prisma.module.ts
    │   └── prisma.service.ts
    └── modules/
        ├── auth/
        │   ├── auth.module.ts
        │   ├── auth.controller.ts
        │   ├── auth.service.ts
        │   ├── dto/
        │   ├── guards/
        │   └── strategies/
        └── <modulo>/
            ├── <modulo>.module.ts
            ├── <modulo>.controller.ts
            ├── <modulo>.service.ts
            ├── dto/
            └── <modulo>.service.spec.ts
```

La estructura interna crece según la necesidad. No es obligatorio crear todos los directorios de ejemplo para cada módulo.

## 4. Responsabilidades

| Elemento | Responsabilidad |
|---|---|
| `main.ts` | Arranca NestJS y configura prefijo HTTP, CORS, validación global y opciones del servidor. |
| `app.module.ts` | Compone los módulos de la aplicación y las dependencias compartidas. |
| `<modulo>.module.ts` | Registra controllers, services y dependencias del módulo. |
| `<modulo>.controller.ts` | Recibe solicitudes HTTP, valida o transforma la entrada mediante DTOs y delega en el service. |
| `<modulo>.service.ts` | Implementa las operaciones y reglas de la funcionalidad; puede usar Prisma directamente en el MVP. |
| `dto/` | Define y valida contratos de entrada y salida del módulo. |
| `database/` | Expone el cliente compartido de Prisma y su ciclo de vida. |
| `common/` | Contiene elementos técnicos transversales realmente compartidos, no lógica específica de una funcionalidad. |
| `config/` | Carga y valida la configuración proveniente del entorno. |

## 5. Flujo normal de una solicitud

```text
HTTP Request
    ↓
Controller
    ↓ DTO validado
Service
    ↓ PrismaService
PostgreSQL
    ↓
Service transforma el resultado
    ↓
Controller responde HTTP
```

El controller no ejecuta consultas Prisma ni concentra reglas de negocio. El service no maneja detalles de transporte HTTP, como cookies o códigos de respuesta, salvo que una decisión de implementación de Nest requiera coordinarse con el adaptador correspondiente.

### Acceso a datos en el MVP

- Los servicios pueden inyectar `PrismaService` y llamar a Prisma directamente.
- Las consultas y transacciones quedan dentro del módulo dueño de la funcionalidad.
- No se añade una capa de repositorios que solo replique llamadas de Prisma.
- Los modelos de Prisma no se exponen automáticamente como contratos HTTP: usar DTOs o mapeo de respuesta cuando el API necesite un formato distinto.
- Las operaciones que deben ser atómicas usan transacciones de Prisma dentro del service responsable.

## 6. Módulos y responsabilidades de negocio

- Los módulos se derivan de funcionalidades expresadas en los requisitos del producto.
- Cada módulo posee sus controllers, services y DTOs.
- Un módulo no importa directamente los services internos de otro módulo. Si aparece una dependencia entre funcionalidades, se evalúa una colaboración explícita a través de los módulos, sin mover prematuramente todo a una capa compartida.
- No se crea un módulo global para alojar lógica que todavía no tiene un dominio claro.
- `common/` no se usa como cajón de sastre para helpers de una sola funcionalidad.

Los nombres de `<modulo>` y `<entidad>` se sustituyen por términos del dominio real cuando el producto y sus casos de uso estén especificados.

## 7. Cuándo añadir más estructura

La simplicidad del MVP no impide evolucionar. Las abstracciones se añaden cuando resuelven un problema observado:

- **Caso de uso independiente:** cuando una operación tiene varios pasos, reglas de negocio complejas, coordinación de dependencias o una responsabilidad que ya no se entiende bien dentro del service.
- **Dominio explícito:** cuando existen invariantes, transiciones o reglas que conviene modelar independientemente de NestJS y Prisma.
- **Repositorio o puerto:** cuando se necesita más de una implementación, una frontera clara con una integración externa, o el acoplamiento a Prisma dificulta de forma concreta las pruebas o los cambios.
- **Adaptador externo:** cuando una funcionalidad integra un proveedor, servicio remoto u otra tecnología sustituible.

La evolución se realiza módulo por módulo. No se requiere crear de antemano `domain/`, `application/`, `adapters/` ni puertos para cada persistencia. No introducir un repositorio genérico como requisito para todos los módulos.

## 8. Módulo de autenticación

La autenticación es un módulo Nest independiente:

```text
modules/auth/
├── auth.module.ts
├── auth.controller.ts
├── auth.service.ts
├── dto/
├── guards/
└── strategies/
```

Los guards y la estrategia JWT protegen endpoints y establecen la identidad autenticada en la solicitud. El módulo implementa el contrato JWT Bearer; no presupone roles, permisos ni atributos de usuario que el producto todavía no haya definido.

### Política de tokens y endpoints

- El access token JWT tiene duración limitada y se envía en `Authorization: Bearer <accessToken>`.
- El refresh token se transmite mediante cookie `HttpOnly`, se rota al renovar y puede revocarse al cerrar sesión.
- Los secretos de firma y la configuración de emisor/audiencia se quedan exclusivamente en el backend.
- Las duraciones, claims y modelo de sesión definitivos se fijarán durante la implementación del proyecto.

| Método | Endpoint | Responsabilidad |
|---|---|---|
| `POST` | `/api/auth/login` | Valida credenciales, devuelve un access token y establece la cookie de refresh. |
| `POST` | `/api/auth/refresh` | Valida y rota la cookie; devuelve un nuevo access token. |
| `POST` | `/api/auth/logout` | Revoca la sesión de refresh y expira la cookie. |
| `GET` | `/api/auth/me` | Devuelve la identidad asociada al Bearer token actual. |

### Flujo de autenticación

1. El cliente envía credenciales a `POST /api/auth/login`.
2. `AuthController` valida el DTO y delega la operación en `AuthService`.
3. `AuthService` valida las credenciales y gestiona la emisión o renovación de tokens.
4. `AuthController` devuelve el access token en la respuesta y establece la cookie de refresh.
5. El frontend mantiene el access token en memoria y lo envía como Bearer en solicitudes protegidas.
6. Al iniciar la aplicación o al recibir un `401`, el frontend solicita `POST /api/auth/refresh` con credenciales de cookie habilitadas.
7. Si el refresh es válido, el backend lo rota y devuelve un access token nuevo; si no, responde `401` y el controller expira la cookie.
8. `POST /api/auth/logout` invalida la sesión de refresh; el controller expira la cookie y el frontend elimina el estado local.

El mecanismo de refresh debe permitir revocación y evitar almacenar el token en claro. El access token se puede mantener válido hasta su expiración; una sesión de refresh revocada no puede obtener tokens nuevos.

### Guards, cookies y CORS

- `JwtAuthGuard` valida el Bearer token en endpoints protegidos.
- Login y refresh se marcan explícitamente como rutas públicas respecto al guard Bearer.
- La cookie de refresh usa `HttpOnly`; en producción también `Secure`.
- `SameSite` se configura según el despliegue. Si se necesita contexto cross-site, usar `None; Secure` y protección CSRF apropiada.
- CORS permite orígenes explícitos y credenciales; no se combina un origen `*` con credenciales.
- Se valida el origen de solicitudes que usan cookies y se limita `Path`/`Domain` a lo necesario.
- La ausencia o invalidez de autenticación produce `401`; la falta de autorización produce `403`.

## 9. Validación, errores y configuración

- Validar DTOs en la frontera HTTP mediante pipes de NestJS.
- Mantener un formato de errores consistente mediante filtros comunes cuando el proyecto lo requiera.
- Cargar configuración desde variables de entorno y validar las requeridas durante el arranque.
- `.env.example` documenta nombres y formatos, nunca valores secretos reales.
- `DATABASE_URL` y las claves JWT se configuran exclusivamente en el backend.
- El prefijo global inicial de API es `/api`.

## 10. Pruebas recomendadas para el MVP

- Probar servicios y reglas funcionales aisladas, usando mocks de `PrismaService` cuando sea suficiente.
- Añadir pruebas de integración para consultas o transacciones cuya interacción con PostgreSQL sea relevante.
- Probar con pruebas end-to-end los contratos HTTP y los flujos críticos de autenticación.
- No crear una estructura de puertos o repositorios solo para facilitar mocks; introducirla cuando el aislamiento aporte valor claro.

## 11. Docker y ejecución

- `Dockerfile` incluye etapas de dependencias, build y ejecución de producción.
- `docker-compose.yaml` proporciona el entorno de desarrollo con hot reload.
- NestJS escucha en `0.0.0.0:3000` dentro del contenedor y publica el puerto `3000`.
- PostgreSQL se ejecuta en su contenedor independiente y se conecta al backend mediante configuración de entorno.
- `.dockerignore` excluye dependencias locales, archivos `.env`, compilaciones y artefactos innecesarios.
- Las migraciones de Prisma se ejecutan mediante comandos de desarrollo o despliegue según el entorno.

## 12. Directrices para agentes de desarrollo

Al utilizar este documento como contexto para construir o modificar un backend:

1. Inspeccionar primero el proyecto, sus dependencias y convenciones. No reemplazar estructura existente sin necesidad y autorización.
2. Tratar el stack de la sección 1 como baseline fijo. Si un requerimiento entra en conflicto con él, describir el conflicto y pedir una decisión antes de cambiarlo.
3. Derivar módulos, entidades, endpoints y reglas de los requisitos concretos. Los placeholders son estructurales y no implican funcionalidades de negocio.
4. Crear solo lo solicitado; no generar módulos, CRUDs, entidades ni capas vacías por anticipado.
5. Usar `controller + service + DTOs` como patrón por defecto. Añadir casos de uso, dominio explícito, puertos o repositorios únicamente ante una necesidad concreta y localizada.
6. Mantener consultas y transacciones en el módulo responsable; no crear una capa común de repositorios por convención.
7. Proteger operaciones en backend y no depender de controles de navegación del frontend.
8. Ejecutar build y pruebas relevantes para el cambio sin incorporar refactors ajenos al alcance.

Esta propuesta fija tecnologías y convenciones de MVP; no define un dominio de negocio. Los requisitos explícitos del producto determinan el software que se construye. Si contradicen esta propuesta, el agente debe señalar el conflicto en vez de tomar una decisión silenciosa.
