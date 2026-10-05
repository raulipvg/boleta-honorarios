# Base de datos

PostgreSQL se inicializa una sola vez sobre un volumen vacío mediante el SQL de baseline `001_mvp_schema.sql`, el catálogo oficial `002_catalog_seed.sql` y `seed-inicial.sql`. `001_mvp_schema.sql` debe mantenerse alineado con la migración inicial de EF Core. Los cambios posteriores del esquema son migraciones versionadas de EF Core, mantenidas en `backend/`; no editar el esquema de una instalación existente directamente.

## Inicialización local

1. Copiar `.env.example` a `.env` y usar `APP_DB_USER`/`APP_DB_PASSWORD` también en `backend/.env`.
2. Generar el hash y el nombre normalizado con la utilidad Identity del backend y establecer `INITIAL_ADMIN_PASSWORD_HASH` y `INITIAL_ADMIN_NORMALIZED_USERNAME`. El administrador seeded debe conocer la contraseña que corresponde al hash y cambiarla en su primer acceso.
3. Iniciar PostgreSQL desde esta carpeta con `docker compose up -d`. La inicialización crea un rol propietario de esquema y otro de runtime con permisos DML, separado del usuario de migración.

El seed de administrador es idempotente y solo crea/asigna el rol a la cuenta cuando inserta esa identidad durante la primera inicialización; no eleva el rol de una cuenta preexistente al repetirse. No guardar contraseñas ni hashes reales en Git. Las tasas iniciales confirmadas son 15,25 % para 2026 y 16,00 % para 2027; las filas son parámetros globales de lectura y no se editan desde la API.

Para reiniciar una base local desde cero se debe eliminar explícitamente el volumen de desarrollo. No aplicar esa operación en ambientes con datos.
