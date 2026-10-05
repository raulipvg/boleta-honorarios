-- Parámetros globales de catálogo. No hay CRUD de roles ni de tasas en la API.
BEGIN;

INSERT INTO roles (codigo, nombre, normalized_name, concurrency_stamp)
VALUES
    ('ADMINISTRADOR', 'Administrador', 'ADMINISTRADOR', gen_random_uuid()::text),
    ('PROFESIONAL', 'Profesional', 'PROFESIONAL', gen_random_uuid()::text)
ON CONFLICT (codigo) DO NOTHING;

-- Tasas oficiales confirmadas por el producto.
INSERT INTO retencion_boleta_honorarios (anio, porcentaje)
VALUES (2026, 15.25), (2027, 16.00)
ON CONFLICT (anio) DO UPDATE
SET porcentaje = EXCLUDED.porcentaje;

COMMIT;
