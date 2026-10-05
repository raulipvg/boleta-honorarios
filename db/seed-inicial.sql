-- Ejecutar solo durante la inicialización de una base vacía, después del esquema.
-- Los valores se inyectan mediante variables de psql; nunca guardar credenciales aquí.
-- Ejemplo: psql -v admin_username=admin -v admin_normalized_username=ADMIN -v admin_password_hash=<hash> -f seed-inicial.sql

BEGIN;

WITH administrador AS (
    INSERT INTO usuarios (
        user_name,
        normalized_user_name,
        password_hash,
        security_stamp,
        concurrency_stamp,
        email_confirmed,
        phone_number_confirmed,
        two_factor_enabled,
        lockout_enabled,
        access_failed_count,
        activo,
        cambio_contrasena_requerido
    )
    VALUES (
        :'admin_username',
        :'admin_normalized_username',
        :'admin_password_hash',
        gen_random_uuid()::text,
        gen_random_uuid()::text,
        false,
        false,
        false,
        true,
        0,
        true,
        true
    )
    ON CONFLICT (normalized_user_name) DO NOTHING
    RETURNING id
)
INSERT INTO usuarios_roles (usuario_id, rol_id)
SELECT u.id, r.id
FROM administrador u
JOIN roles r ON r.codigo = 'ADMINISTRADOR'
ON CONFLICT (usuario_id, rol_id) DO NOTHING;

COMMIT;
