# Arquitectura estándar del Frontend

Este documento define el stack, la estructura estándar y las responsabilidades del frontend. También describe la parte del flujo JWT Bearer que implementa la aplicación cliente.

## 1. Stack tecnológico

- **Runtime de desarrollo y build:** Node.js 24.
- **Framework de interfaz:** React 19.1.0.
- **Lenguaje:** TypeScript.
- **Bundler y servidor de desarrollo:** Vite.
- **Componentes de interfaz:** Ant Design 6.6.5.
- **Routing:** React Router para organizar rutas públicas y protegidas.
- **Cliente HTTP:** Axios mediante un cliente centralizado.
- **Contenedores:** Docker y Docker Compose.

Las versiones de las dependencias se fijan en `package.json` y `package-lock.json`.

## 2. Responsabilidades

- Presentar la interfaz y la navegación.
- Organizar pantallas y componentes por funcionalidad.
- Consumir la API del backend mediante servicios tipados.
- Mantener en memoria el estado de autenticación del usuario.
- Enviar el access token como JWT Bearer en las solicitudes protegidas.
- Facilitar la navegación protegida; la autorización real siempre se valida en el backend.

## 3. Estructura del proyecto

```text
frontend/
├── Dockerfile
├── compose.yaml
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
    │   └── AuthContext.tsx
    ├── hooks/
    ├── pages/
    │   ├── Login/
    │   └── <modulo>/
    ├── routes/
    ├── services/
    │   ├── apiClient.ts
    │   └── authService.ts
    ├── types/
    ├── constants/
    ├── theme/
    └── utils/
```

### Responsabilidad de las carpetas y archivos

- `main.tsx`: monta React y los providers globales.
- `App.tsx` y `routes/`: componen la aplicación y declaran las rutas.
- `components/`: contiene layout y componentes reutilizables.
- `pages/`: agrupa pantallas por módulo o funcionalidad.
- `context/AuthContext.tsx`: mantiene el estado de sesión en memoria y expone operaciones de autenticación.
- `services/apiClient.ts`: configura Axios, agrega el Bearer token y centraliza el manejo de errores HTTP.
- `services/authService.ts`: agrupa las llamadas de login, refresh, logout y consulta de identidad.
- `ProtectedRoute.tsx`: controla la navegación visual hacia páginas privadas; no es una barrera de seguridad para la API.
- `hooks/`, `types/`, `constants/`, `theme/` y `utils/`: reúnen lógica compartida, tipos, constantes, estilos y funciones auxiliares.

## 4. Estructura recomendada por módulo

```text
src/pages/<modulo>/
├── index.tsx
├── components/
├── hooks/
│   └── <modulo>.types.ts
├── <modulo>.constants.ts   # opcional
└── utils.ts                # opcional
```

La página del módulo compone la vista. La lógica específica se distribuye en hooks y servicios; los componentes se mantienen preferentemente presentacionales. No se crean abstracciones compartidas grandes hasta comprobar que existe una duplicación real.

## 5. Routing y layout

- Las rutas públicas incluyen, como mínimo, `/login`.
- Las rutas privadas usan `ProtectedRoute` y el estado de autenticación del contexto.
- El layout global se compone desde `App.tsx` o desde un componente de layout dedicado.
- La navegación protegida mejora la experiencia de usuario, pero no sustituye los guards del backend.

## 6. Autenticación en el frontend

### Archivos principales

- `src/context/AuthContext.tsx`
- `src/services/authService.ts`
- `src/services/apiClient.ts`
- `src/components/ProtectedRoute.tsx`
- `src/pages/Login/`

### Política de tokens

- El access token se mantiene **solo en memoria**, dentro del estado de autenticación; no se persiste en `localStorage` ni `sessionStorage`.
- El refresh token se gestiona mediante una cookie `HttpOnly` establecida por el backend. El código JavaScript del frontend no puede leerla.
- Las solicitudes a rutas protegidas envían `Authorization: Bearer <accessToken>`.
- Las llamadas que necesitan enviar la cookie habilitan las credenciales HTTP (`withCredentials` en Axios).

### Flujo de login

1. El usuario ingresa sus credenciales en la pantalla de login.
2. `authService` envía `POST /api/auth/login` con credenciales de cookie habilitadas.
3. Si las credenciales son válidas, el backend devuelve un access token y establece la cookie de refresh.
4. `AuthContext` mantiene el token en memoria y actualiza el estado de autenticación.
5. El cliente HTTP agrega el access token Bearer a las llamadas protegidas.

### Restauración y renovación de sesión

1. Al cargar la aplicación, el frontend llama `POST /api/auth/refresh` con credenciales de cookie habilitadas.
2. Si la cookie de refresh es válida, el backend devuelve un nuevo access token.
3. El frontend mantiene el token en memoria y puede consultar `GET /api/auth/me` para cargar la identidad actual.
4. Si la renovación falla, el frontend limpia el estado de autenticación y muestra o redirige al login.

### Interceptor HTTP

- El interceptor de solicitud agrega el encabezado Bearer cuando hay un access token vigente en memoria.
- Ante un `401`, intenta renovar el token una sola vez y reintenta la solicitud original como máximo una vez.
- Las solicitudes concurrentes que reciben `401` deben compartir una única renovación.
- Si el refresh falla, limpia el estado y lleva al usuario a login.
- Un `403` no dispara renovación; se trata como una respuesta de usuario autenticado sin autorización para la operación.

### Logout

1. El frontend llama `POST /api/auth/logout` con credenciales de cookie habilitadas.
2. Limpia el token y la identidad de `AuthContext`.
3. Navega a la ruta de login.

## 7. API y configuración

- `VITE_API_URL` contiene la URL pública de la API, por ejemplo `http://localhost:3000/api` en desarrollo.
- `VITE_API_URL` no debe contener secretos ni credenciales.
- El cliente HTTP centralizado es el punto de entrada para las llamadas funcionales a la API.
- CORS y cookies con credenciales deben configurarse de forma coordinada con el backend.

## 8. Docker y ejecución

- `Dockerfile` incluye etapas de dependencias, compilación y ejecución de producción.
- `compose.yaml` proporciona el entorno local de desarrollo con hot reload.
- Vite debe escuchar en `0.0.0.0` dentro del contenedor y publicar el puerto `5173`.
- En producción, los archivos compilados se sirven con un servidor web como Nginx.
- `.dockerignore` excluye dependencias locales, compilaciones y archivos innecesarios.
- Las variables requeridas se documentan en `.env.example`; el `.env` local no se versiona.

## 9. Checklist para nuevas funcionalidades

1. Crear la pantalla en `src/pages/<modulo>/`.
2. Separar componentes, hooks y tipos según la responsabilidad.
3. Consumir endpoints a través de `apiClient.ts` y servicios tipados.
4. Usar `ProtectedRoute` solo para controlar navegación; proteger la operación correspondiente en el backend.
5. Mantener access tokens en memoria y no duplicar lógica de sesión dentro de módulos funcionales.
