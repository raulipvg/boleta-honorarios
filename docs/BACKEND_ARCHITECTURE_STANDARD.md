# Propuesta técnica base: Backend

Este documento sirve como contexto técnico para construir backends con el stack acordado. Define una arquitectura modular hexagonal, convenciones de autenticación y directrices para agentes de desarrollo. Los nombres `<modulo>`, `<entidad>` y `<operacion>` son placeholders, no funcionalidades que deban crearse automáticamente.

## 1. Stack tecnológico

- **Runtime:** Node.js 24.
- **Framework API:** NestJS 12.1.1.
- **Lenguaje:** TypeScript.
- **Transporte HTTP:** API REST sobre HTTP/JSON, usando el adaptador Express predeterminado de NestJS.
- **Persistencia:** PostgreSQL y Prisma.
- **Autenticación:** JWT Bearer para los endpoints protegidos y refresh token mediante cookie `HttpOnly`.
- **Contenedores:** Docker y Docker Compose.

Este stack es la base tecnológica fija de la propuesta. Las versiones acordadas se fijan en `package.json` y `package-lock.json`; no se cambian sin una decisión explícita del proyecto. La base PostgreSQL se ejecuta en un contenedor independiente del backend.

## 2. Objetivos y principios

- Organizar el código por módulos de negocio; cada módulo contiene sus propias capas y puertos.
- Mantener el dominio independiente de NestJS, HTTP, Prisma y PostgreSQL.
- Hacer que los casos de uso dependan de contratos, no de adaptadores concretos.
- Mantener Prisma y otras tecnologías externas en adaptadores o infraestructura.
- Usar un caso de uso explícito por operación relevante, en vez de concentrar muchas operaciones en un servicio monolítico.
- Mantener los controladores HTTP como adaptadores de entrada delgados.
- Hacer que las dependencias entre módulos de negocio pasen por contratos explícitos, no por sus implementaciones internas.
- Validar autenticación y autorización en el backend; los controles de interfaz del frontend no sustituyen esta validación.

## 3. Capas y dirección de dependencias

Cada módulo es una unidad funcional con dominio, aplicación y adaptadores. Los adaptadores traducen entre el exterior y los contratos definidos hacia el interior:

```text
                         ┌───────────────────────────┐
                         │ Adaptador de entrada      │
                         │ HTTP / Controller / DTO   │
                         └─────────────┬─────────────┘
                                       │ invoca
                                       ▼
                         ┌───────────────────────────┐
                         │ Aplicación                │
                         │ Puertos de entrada        │
                         │ Casos de uso              │
                         └─────────────┬─────────────┘
                                       │ usa contratos
                                       ▼
                         ┌───────────────────────────┐
                         │ Dominio                   │
                         │ Entidades / Reglas        │
                         │ Puertos de salida         │
                         └─────────────▲─────────────┘
                                       │ implementa
                         ┌─────────────┴─────────────┐
                         │ Adaptador de salida       │
                         │ Repositorio Prisma        │
                         └─────────────┬─────────────┘
                                       │ usa
                                       ▼
                         ┌───────────────────────────┐
                         │ Infraestructura           │
                         │ Prisma / PostgreSQL       │
                         └───────────────────────────┘
```

La inversión de dependencias significa que el caso de uso conoce el puerto —el contrato que necesita—, pero no la clase que lo implementa. El adaptador Prisma implementa ese puerto y se conecta a la infraestructura de base de datos.

### Reglas por capa

| Capa                   | Puede conocer                                                  | No debe conocer                               |
| ---------------------- | -------------------------------------------------------------- | --------------------------------------------- |
| `domain/`            | Entidades, reglas, value objects y puertos del dominio         | NestJS, HTTP, Prisma, PostgreSQL              |
| `application/`       | Casos de uso, comandos y contratos necesarios para ejecutarlos | Prisma, HTTP, detalles de persistencia        |
| `adapters/inbound/`  | NestJS, HTTP, DTOs, guards y traducción de solicitudes        | Implementaciones de persistencia directamente |
| `adapters/outbound/` | Tecnologías externas necesarias para implementar puertos      | Reglas de negocio que pertenecen al dominio   |
| `infrastructure/`    | Clientes y configuración técnica compartida                  | Casos de uso o reglas de negocio              |

## 4. Estructura del backend

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
    ├── infrastructure/
    │   └── database/
    │       └── prisma/
    │           ├── prisma.module.ts
    │           └── prisma.service.ts
    └── modules/
        ├── auth/
        ├── <modulo-a>/
        └── <modulo-b>/
```

Los nombres entre ángulos son placeholders estructurales. Los módulos de negocio se crean únicamente cuando los requisitos del software definan sus responsabilidades. Cada módulo mantiene su propia arquitectura:

```text
modules/<modulo>/
├── domain/
│   ├── entities/
│   ├── value-objects/
│   ├── services/                  # solo lógica propia del dominio
│   └── ports/
│       └── outbound/              # contratos que el dominio necesita
├── application/
│   ├── ports/
│   │   └── inbound/               # operaciones ofrecidas por la aplicación
│   └── use-cases/
├── adapters/
│   ├── inbound/
│   │   └── http/
│   │       ├── <modulo>.controller.ts
│   │       └── dto/
│   └── outbound/
│       └── persistence/
│           └── prisma/
│               └── prisma-<modulo>.repository.ts
└── <modulo>.module.ts
```

La forma concreta de cada módulo puede crecer según sus necesidades. No se crean carpetas vacías o capas sin responsabilidad real.

## 5. Responsabilidades de las áreas compartidas

### `main.ts` y `app.module.ts`

- `main.ts` configura el arranque HTTP, el prefijo global `/api`, CORS, validación global y demás configuración de NestJS.
- `app.module.ts` compone los módulos funcionales y la infraestructura compartida.

### `common/`

Contiene únicamente comportamiento transversal y no específico de un dominio, como filtros HTTP globales, interceptores, pipes y decorators realmente compartidos. No debe convertirse en un cajón de sastre para servicios de negocio, helpers de un módulo o utilidades sin dueño claro.

### `config/`

Carga y valida variables de entorno y configuración técnica. Los secretos se proporcionan desde el entorno y nunca se versionan ni se exponen al frontend.

### `infrastructure/database/prisma/`

- `PrismaService` administra el cliente compartido de Prisma y su ciclo de vida.
- `PrismaModule` expone ese servicio a los adaptadores de persistencia.
- El esquema y las migraciones se mantienen en `backend/prisma/`.
- Los repositorios concretos de cada dominio no se colocan aquí: viven en el adaptador de salida del módulo correspondiente.

## 6. Puertos, adaptadores e inversión de dependencias

### Puerto de salida

Un puerto de salida expresa una necesidad del módulo mediante un contrato propio, sin describir cómo se implementa. Por ejemplo, si una operación requiere persistir una entidad, el módulo declara su propio puerto en `domain/ports/outbound/`. Los nombres y métodos del contrato se derivan del lenguaje y las reglas del dominio real.

El contrato pertenece al módulo que lo necesita. No se crea un repositorio universal compartido solo para evitar declarar puertos específicos.

### Caso de uso

Cada operación relevante se representa con un caso de uso específico, por ejemplo `Crear<Entidad>UseCase` u `<Operacion><Entidad>UseCase`. El caso de uso coordina reglas del dominio y puertos requeridos, pero no conoce HTTP ni ejecuta consultas Prisma.

Por decisión de este proyecto, los casos de uso usan `@Injectable()` de NestJS y los tokens de inyección necesarios. Esto introduce un acoplamiento puntual con NestJS en la configuración de dependencias de `application/`; la lógica del caso de uso y sus contratos siguen dependiendo de puertos, no de adaptadores concretos.

Los casos de uso no deben importar controladores, DTOs HTTP, `PrismaService` ni modelos generados por Prisma. Las clases y los contratos concretos se nombran usando el dominio solicitado para cada aplicación.

### Adaptador de persistencia

El repositorio Prisma de un módulo implementa el puerto de salida definido por ese módulo. Traduce entre el modelo de dominio y el esquema de persistencia; las llamadas a `PrismaService` se limitan a este adaptador.

La traducción en ambos sentidos evita que los modelos generados por Prisma se filtren hacia los casos de uso o las entidades del dominio.

### Composición en NestJS

El módulo Nest del dominio registra el caso de uso y enlaza el token del puerto con el adaptador correspondiente, normalmente mediante `useClass`. El token identifica el contrato; no debe revelar detalles del adaptador en la capa de aplicación.

Esta composición permite sustituir el adaptador de persistencia por un fake, mock u otra implementación sin cambiar la lógica del caso de uso.

## 7. Organización de los casos de uso

Evitar un servicio de módulo que acumule operaciones no relacionadas. Preferir clases específicas, cada una con una responsabilidad identificable. Los nombres se construyen con la acción y el concepto real del dominio, por ejemplo:

```text
application/use-cases/
├── crear-<entidad>.use-case.ts
├── actualizar-<entidad>.use-case.ts
├── obtener-<entidad>.use-case.ts
└── listar-<entidades>.use-case.ts
```

La lista es ilustrativa, no un CRUD obligatorio. Los casos de uso reflejan operaciones que existen en los requisitos y reglas reales; no se generan operaciones vacías ni se asume que toda entidad necesita crear, actualizar, listar y eliminar.

El controller pertenece al adaptador HTTP: transforma DTOs en comandos o parámetros de aplicación, llama al caso de uso y transforma el resultado en una respuesta HTTP. No construye entidades de Prisma ni contiene lógica de negocio.

## 8. Módulo de autenticación

La autenticación también se organiza dentro de `modules/`; no se mantiene como un subsistema paralelo en `src/auth/`:

```text
modules/auth/
├── domain/
│   ├── entities/
│   ├── value-objects/
│   └── ports/
│       └── outbound/
├── application/
│   ├── ports/
│   │   └── inbound/
│   └── use-cases/
│       ├── login.use-case.ts
│       ├── refresh-token.use-case.ts
│       └── logout.use-case.ts
├── adapters/
│   ├── inbound/
│   │   └── http/
│   │       ├── auth.controller.ts
│   │       ├── dto/
│   │       ├── guards/
│   │       ├── strategies/
│   │       └── decorators/
│   └── outbound/
│       ├── jwt/
│       ├── password/
│       └── persistence/
│           └── prisma/
│               └── prisma-auth.repository.ts
└── auth.module.ts
```

Los guards, strategies, decorators HTTP y componentes específicos de NestJS/Passport se mantienen en el borde de entrada. La emisión de tokens, el hashing de contraseñas y el acceso a persistencia se conectan a la aplicación mediante los puertos correspondientes.

### Política JWT y endpoints de referencia

- El access token tiene duración limitada y se envía en `Authorization: Bearer <accessToken>`.
- El refresh token se transmite en una cookie `HttpOnly`, se rota al renovar y puede revocarse en logout.
- Los secretos de firma y configuración de emisor/audiencia se guardan solo en el backend.
- Las duraciones y claims definitivos se fijarán al implementar la autenticación.

| Método  | Endpoint              | Responsabilidad                                                      |
| -------- | --------------------- | -------------------------------------------------------------------- |
| `POST` | `/api/auth/login`   | Valida credenciales, emite access JWT y establece cookie de refresh. |
| `POST` | `/api/auth/refresh` | Valida y rota la cookie; devuelve un nuevo access JWT.               |
| `POST` | `/api/auth/logout`  | Revoca la sesión de refresh y expira la cookie.                     |
| `GET`  | `/api/auth/me`      | Devuelve la identidad asociada al Bearer token.                      |

### Flujo de autenticación

1. El frontend envía credenciales a `POST /api/auth/login`.
2. El controller valida el DTO y llama al caso de uso de login.
3. El caso de uso valida la identidad usando los puertos requeridos y solicita la emisión del token mediante su contrato de salida.
4. Los adaptadores de salida implementan hashing, firma JWT y persistencia de la sesión de refresh.
5. El backend devuelve el access token y establece la cookie de refresh.
6. El frontend envía el access token como Bearer en las llamadas protegidas.
7. Ante un access token expirado, el frontend solicita `POST /api/auth/refresh`; el backend valida y rota el refresh token y emite un nuevo access token.
8. En logout, el caso de uso revoca la sesión, el adaptador HTTP expira la cookie y el frontend elimina su estado en memoria.

El mecanismo persistente del refresh debe permitir revocación y evitar almacenar el token en claro, por ejemplo guardando una sesión revocable o el hash del token. La autenticación responde `401` ante identidad ausente o inválida; la autorización responde `403` cuando la identidad no puede realizar la operación.

### Guards, cookies y CORS

- `JwtAuthGuard` y la estrategia JWT validan el access token en rutas protegidas y dejan la identidad autenticada disponible para la solicitud.
- Login y refresh se marcan explícitamente como rutas públicas respecto al guard Bearer.
- La cookie de refresh usa `HttpOnly`; en producción también `Secure`.
- `SameSite` se configura según el despliegue; si se requiere contexto cross-site, usar `None; Secure` y protección CSRF apropiada.
- CORS admite orígenes explícitos y credenciales; no se combina un origen `*` con credenciales.
- Validar el origen de las solicitudes que usan cookies y limitar `Path`/`Domain` a lo necesario.

## 9. Módulos de negocio y límites entre dominios

Cada módulo conserva sus entidades, casos de uso, puertos y adaptadores. Los módulos no importan repositorios ni servicios internos de otros módulos. Si una funcionalidad necesita colaborar con otra, la integración se define mediante un contrato explícito y se mantiene dentro del límite del módulo responsable.

Los límites y nombres de módulos se derivan del lenguaje, las reglas y los requisitos del producto. Los placeholders de esta propuesta ilustran la forma de organizar el código; no determinan los dominios del software que se construya.

## 10. Flujo recomendado para incorporar una funcionalidad

1. Identificar el módulo de dominio responsable.
2. Modelar entidades, value objects y reglas en `domain/`.
3. Definir en `domain/ports/outbound/` los contratos externos que el dominio necesita.
4. Definir en `application/ports/inbound/` las operaciones ofrecidas y crear un caso de uso por operación.
5. Implementar el adaptador de entrada, normalmente HTTP, con controller y DTOs.
6. Implementar los adaptadores de salida necesarios, como un repositorio Prisma dentro del módulo.
7. Registrar los casos de uso y enlazar tokens de puertos a sus implementaciones en `<modulo>.module.ts`.
8. Probar el dominio y los casos de uso usando implementaciones de prueba de sus puertos; probar los adaptadores por separado.

## 11. Pruebas y checklist de consistencia

- **Dominio:** pruebas unitarias de entidades, value objects y reglas sin levantar NestJS ni Prisma.
- **Casos de uso:** pruebas unitarias con repositorios u otros puertos simulados.
- **Adaptadores Prisma:** pruebas de integración para verificar la traducción entre persistencia y dominio.
- **Adaptadores HTTP:** pruebas de controller o end-to-end para contratos, guards y códigos HTTP.

Antes de considerar un módulo consistente, comprobar que:

- `domain/` no importa NestJS, Prisma, HTTP ni paquetes de PostgreSQL.
- Los casos de uso dependen de puertos y no llaman directamente a Prisma.
- Los repositorios Prisma concretos están dentro del adaptador de salida del módulo.
- Los controllers no contienen reglas de negocio ni acceso a persistencia.
- Los tokens de Nest conectan puertos con implementaciones en el módulo.
- `common/` no contiene lógica propia de un dominio.
- Los guards del backend protegen las operaciones independientemente de la navegación del frontend.
- Ningún secreto aparece en código, respuestas HTTP, logs ni archivos versionados.

## 12. Docker y configuración

- `Dockerfile` incluye etapas de dependencias, build y ejecución de producción.
- `docker-compose.yaml` proporciona el entorno de desarrollo con hot reload.
- NestJS escucha en `0.0.0.0:3000` dentro del contenedor y publica el puerto `3000`.
- PostgreSQL se ejecuta en su contenedor independiente y se conecta al backend mediante la configuración del entorno.
- `DATABASE_URL` y las claves JWT son configuración exclusiva del backend.
- `.env.example` documenta variables esperadas sin incluir valores secretos reales; `.env` local no se versiona.
- Las migraciones de Prisma se ejecutan mediante comandos de desarrollo o despliegue según el entorno.

## 13. Directrices para agentes de desarrollo

Al utilizar este documento como contexto para construir o modificar un backend:

1. Inspeccionar primero los archivos, dependencias, patrones y límites existentes. No reemplazar una arquitectura funcional sin una necesidad relacionada con la tarea y autorización explícita.
2. Tratar el stack de la sección 1 como baseline fijo. Si un requerimiento del proyecto entra en conflicto con él, exponer el conflicto y pedir una decisión antes de cambiar tecnología o versiones.
3. Derivar módulos, entidades, value objects, puertos, casos de uso y endpoints de los requisitos proporcionados. No tratar placeholders ni ejemplos estructurales como requisitos de negocio.
4. Crear solo los módulos y capas que necesita el alcance solicitado; no generar módulos vacíos ni CRUDs no requeridos.
5. Mantener las reglas de dominio en `domain/`, la coordinación en casos de uso y la interacción con tecnologías externas en adaptadores o infraestructura.
6. No permitir que Prisma, DTOs HTTP o modelos de transporte se filtren hacia el dominio o los casos de uso.
7. Evitar dependencias directas entre implementaciones internas de distintos módulos; definir contratos explícitos cuando exista una colaboración requerida.
8. Aplicar guards y validaciones en el backend a toda operación protegida. La navegación o visibilidad del frontend nunca basta para autorizar una operación.
9. Ejecutar las verificaciones y pruebas relacionadas con el cambio, sin introducir refactors ajenos al alcance.

Esta propuesta fija límites técnicos y convenciones, no modelos de negocio. Los requisitos del producto determinan qué software se construye; si una instrucción específica contradice este baseline, el agente debe señalar la diferencia en lugar de asumir una decisión silenciosa.
