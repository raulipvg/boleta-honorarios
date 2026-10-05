-- Esquema inicial PostgreSQL para el MVP de Gestión de ingresos por honorarios.
-- Ejecutar una vez sobre una base vacía; cambios posteriores deben gestionarse
-- mediante migraciones versionadas de EF Core.
--
-- Los totales de PERIODO_INSTITUCION y PERIODO_MENSUAL son agregados mantenidos
-- por el servicio de aplicación en la misma transacción que modifica las horas.
-- No se usan triggers para duplicar esa lógica.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE usuarios (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    identity_subject varchar(255) NOT NULL UNIQUE,
    password_hash text NULL,
    activo boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_usuarios_identity_subject_not_blank
        CHECK (length(btrim(identity_subject)) > 0)
);

COMMENT ON COLUMN usuarios.password_hash IS
    'Hash de contraseña producido por ASP.NET Core Identity; nunca almacenar texto plano. NULL cuando la autenticación se delega a OIDC.';

CREATE TABLE roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL UNIQUE,
    nombre varchar(100) NOT NULL,
    CONSTRAINT ck_roles_codigo
        CHECK (codigo IN ('ADMINISTRADOR', 'PROFESIONAL'))
);

CREATE TABLE usuarios_roles (
    usuario_id uuid NOT NULL,
    rol_id uuid NOT NULL,
    asignado_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (usuario_id, rol_id),
    CONSTRAINT fk_usuarios_roles_usuario
        FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE CASCADE,
    CONSTRAINT fk_usuarios_roles_rol
        FOREIGN KEY (rol_id) REFERENCES roles (id) ON DELETE RESTRICT
);

CREATE TABLE profesionales (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL UNIQUE,
    nombre varchar(200) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_profesionales_usuario
        FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_profesionales_nombre_not_blank
        CHECK (length(btrim(nombre)) > 0)
);

CREATE TABLE instituciones_publicas (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    nombre varchar(200) NOT NULL,
    activa boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_instituciones_publicas_nombre_not_blank
        CHECK (length(btrim(nombre)) > 0)
);

CREATE INDEX ix_instituciones_publicas_activa_nombre
    ON instituciones_publicas (activa, nombre);

CREATE TABLE profesionales_instituciones (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    profesional_id uuid NOT NULL,
    institucion_publica_id uuid NOT NULL,
    activa boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_profesionales_instituciones_profesional
        FOREIGN KEY (profesional_id) REFERENCES profesionales (id) ON DELETE RESTRICT,
    CONSTRAINT fk_profesionales_instituciones_institucion
        FOREIGN KEY (institucion_publica_id) REFERENCES instituciones_publicas (id) ON DELETE RESTRICT,
    CONSTRAINT uq_profesionales_instituciones_propietario_institucion
        UNIQUE (profesional_id, institucion_publica_id),
    CONSTRAINT uq_profesionales_instituciones_id_propietario
        UNIQUE (id, profesional_id)
);

CREATE INDEX ix_profesionales_instituciones_profesional_activa
    ON profesionales_instituciones (profesional_id, activa);

CREATE TABLE tarifas_hora_anuales_versiones (
    profesional_institucion_id uuid NOT NULL,
    anio smallint NOT NULL,
    version integer NOT NULL,
    valor_hora_clp bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (profesional_institucion_id, anio, version),
    CONSTRAINT fk_tarifas_hora_profesional_institucion
        FOREIGN KEY (profesional_institucion_id)
        REFERENCES profesionales_instituciones (id) ON DELETE RESTRICT,
    CONSTRAINT ck_tarifas_hora_anio
        CHECK (anio BETWEEN 1900 AND 32767),
    CONSTRAINT ck_tarifas_hora_version
        CHECK (version >= 1),
    CONSTRAINT ck_tarifas_hora_valor_no_negativo
        CHECK (valor_hora_clp >= 0)
);

COMMENT ON TABLE tarifas_hora_anuales_versiones IS
    'Tarifa privada por profesional-institución y año. Solo el profesional propietario puede crear nuevas versiones; la API debe aplicar esta autorización.';

CREATE TABLE retencion_boleta_honorarios (
    anio smallint PRIMARY KEY,
    porcentaje numeric(7, 4) NOT NULL,
    CONSTRAINT ck_retencion_boleta_anio
        CHECK (anio BETWEEN 1900 AND 32767),
    CONSTRAINT ck_retencion_boleta_porcentaje
        CHECK (porcentaje BETWEEN 0 AND 100)
);

COMMENT ON TABLE retencion_boleta_honorarios IS
    'Parámetro legal global: una fila por año, sin propietario, versión ni usuario creador. Mantener mediante migración/seed controlado; no editar desde la aplicación.';

CREATE TABLE periodos_mensuales (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    profesional_id uuid NOT NULL,
    anio smallint NOT NULL,
    mes smallint NOT NULL,
    retencion_porcentaje_aplicado numeric(7, 4) NOT NULL,
    total_horas bigint NOT NULL DEFAULT 0,
    bruto_total_clp bigint NOT NULL DEFAULT 0,
    retencion_total_clp bigint NOT NULL DEFAULT 0,
    liquido_total_clp bigint NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_periodos_mensuales_profesional
        FOREIGN KEY (profesional_id) REFERENCES profesionales (id) ON DELETE RESTRICT,
    CONSTRAINT fk_periodos_mensuales_retencion_anual
        FOREIGN KEY (anio) REFERENCES retencion_boleta_honorarios (anio) ON DELETE RESTRICT,
    CONSTRAINT uq_periodos_mensuales_profesional_anio_mes
        UNIQUE (profesional_id, anio, mes),
    CONSTRAINT uq_periodos_mensuales_id_propietario_anio
        UNIQUE (id, profesional_id, anio),
    CONSTRAINT ck_periodos_mensuales_mes
        CHECK (mes BETWEEN 1 AND 12),
    CONSTRAINT ck_periodos_mensuales_retencion_aplicada
        CHECK (retencion_porcentaje_aplicado BETWEEN 0 AND 100),
    CONSTRAINT ck_periodos_mensuales_totales_no_negativos
        CHECK (
            total_horas >= 0
            AND bruto_total_clp >= 0
            AND retencion_total_clp >= 0
            AND liquido_total_clp >= 0
        ),
    CONSTRAINT ck_periodos_mensuales_liquido_consistente
        CHECK (
            retencion_total_clp <= bruto_total_clp
            AND liquido_total_clp = bruto_total_clp - retencion_total_clp
        )
);

CREATE INDEX ix_periodos_mensuales_profesional_fecha
    ON periodos_mensuales (profesional_id, anio DESC, mes DESC);

CREATE TABLE periodos_instituciones (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    periodo_id uuid NOT NULL,
    profesional_id uuid NOT NULL,
    anio smallint NOT NULL,
    profesional_institucion_id uuid NOT NULL,
    tarifa_hora_version integer NOT NULL,
    total_horas bigint NOT NULL DEFAULT 0,
    bruto_total_clp bigint NOT NULL DEFAULT 0,
    retencion_total_clp bigint NOT NULL DEFAULT 0,
    liquido_total_clp bigint NOT NULL DEFAULT 0,
    orden integer NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_periodos_instituciones_periodo_propietario_anio
        FOREIGN KEY (periodo_id, profesional_id, anio)
        REFERENCES periodos_mensuales (id, profesional_id, anio) ON DELETE RESTRICT,
    CONSTRAINT fk_periodos_instituciones_relacion_propietario
        FOREIGN KEY (profesional_institucion_id, profesional_id)
        REFERENCES profesionales_instituciones (id, profesional_id) ON DELETE RESTRICT,
    CONSTRAINT fk_periodos_instituciones_tarifa_aplicada
        FOREIGN KEY (profesional_institucion_id, anio, tarifa_hora_version)
        REFERENCES tarifas_hora_anuales_versiones (profesional_institucion_id, anio, version) ON DELETE RESTRICT,
    CONSTRAINT uq_periodos_instituciones_periodo_relacion
        UNIQUE (periodo_id, profesional_institucion_id),
    CONSTRAINT ck_periodos_instituciones_totales_no_negativos
        CHECK (
            total_horas >= 0
            AND bruto_total_clp >= 0
            AND retencion_total_clp >= 0
            AND liquido_total_clp >= 0
        ),
    CONSTRAINT ck_periodos_instituciones_liquido_consistente
        CHECK (
            retencion_total_clp <= bruto_total_clp
            AND liquido_total_clp = bruto_total_clp - retencion_total_clp
        )
);

CREATE INDEX ix_periodos_instituciones_profesional_anio
    ON periodos_instituciones (profesional_id, anio);

CREATE TABLE registros_horas (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    periodo_institucion_id uuid NOT NULL,
    horas integer NOT NULL,
    orden integer NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_registros_horas_periodo_institucion
        FOREIGN KEY (periodo_institucion_id)
        REFERENCES periodos_instituciones (id) ON DELETE RESTRICT,
    CONSTRAINT ck_registros_horas_minimo_uno
        CHECK (horas >= 1),
    CONSTRAINT uq_registros_horas_orden
        UNIQUE (periodo_institucion_id, orden)
);

CREATE TABLE sesiones_auth (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL,
    sid uuid NOT NULL UNIQUE,
    familia_refresh_id uuid NOT NULL,
    refresh_token_hash varchar(128) NOT NULL UNIQUE,
    creada_at timestamptz NOT NULL DEFAULT now(),
    ultimo_uso_at timestamptz NOT NULL DEFAULT now(),
    expira_inactividad_at timestamptz NOT NULL,
    expira_absoluta_at timestamptz NOT NULL,
    revocada_at timestamptz,
    CONSTRAINT fk_sesiones_auth_usuario
        FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_sesiones_auth_expiraciones
        CHECK (expira_inactividad_at <= expira_absoluta_at)
);

CREATE INDEX ix_sesiones_auth_usuario_vigencia
    ON sesiones_auth (usuario_id, expira_absoluta_at)
    WHERE revocada_at IS NULL;

-- Roles de sistema. No existe una tabla de permisos ni de relación rol-permiso;
-- las políticas ADMINISTRADOR/PROFESIONAL se resuelven en el backend.
INSERT INTO roles (codigo, nombre)
VALUES
    ('ADMINISTRADOR', 'Administrador'),
    ('PROFESIONAL', 'Profesional')
ON CONFLICT (codigo) DO NOTHING;

-- No insertar aquí tasas de ejemplo: los porcentajes oficiales se incorporan
-- mediante una migración/seed controlado tras su validación.

COMMIT;
