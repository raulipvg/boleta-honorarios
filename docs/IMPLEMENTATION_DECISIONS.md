# Decisiones de implementación — MVP de honorarios

Este registro complementa el PRD y los estándares de arquitectura. Los requisitos de seguridad no modificados siguen aplicando.

## Estructura y patrón

- Monolito modular distribuido en `db/`, `backend/` y `frontend/`; se conservan los documentos del producto bajo `docs/`.
- Backend por capas: API → Application → Domain; API registra Infrastructure como composition root. Domain no depende de ASP.NET Core ni EF Core; controllers no contienen reglas.
- La interfaz se organiza por flujos funcionales y consume la API por servicios centralizados.
- `react-router-dom` queda en 7.18.4, superior al baseline 7.13.0 del estándar por vulnerabilidades altas detectadas en la versión base durante la instalación.
- Cada carpeta de componente dispone de su propio Docker Compose. Se coordinan mediante la red Docker externa `gestion-honorarios-net`.
- La migración inicial EF Core es la fuente del modelo; `db/001_mvp_schema.sql` es el baseline de instalación alineado. Las migraciones siguientes residen en `backend/`.

## Autenticación acordada

- ASP.NET Core Identity local, acceso por nombre de usuario y contraseña. No hay registro público.
- Solo Administrador crea cuentas; entrega contraseña temporal y el primer acceso obliga a cambiarla.
- El usuario puede cambiar su contraseña desde su perfil; Administrador puede restablecerla con contraseña temporal.
- No se implementa recuperación autónoma ni MFA. La ausencia de MFA, incluso para Administrador, es una desviación explícita del estándar de backend aprobada por el producto.
- El primer Administrador se inserta mediante seed SQL. El hash Identity se inyecta durante la inicialización y no se versiona junto al código.
- En el Compose local enlazado exclusivamente a `127.0.0.1`, la cookie antifalsificación usa modo `SameAsRequest` para permitir desarrollo HTTP. Producción usa `__Host-Antiforgery` y `Secure` obligatorio; el refresh conserva `__Host-RefreshToken`.
- En desarrollo, el key ring de Data Protection se conserva en el volumen local. Producción requiere un key ring persistente y cifrado con un certificado dedicado entregado por el gestor de secretos.
- JWT se firma con RS256 y llave RSA mínima de 3072 bits. La configuración puede incluir `Jwt:PreviousKeys` (pares `KeyId`/`PublicKeyPath`) para aceptar llaves públicas anteriores durante la ventana de expiración antes de retirarlas. El Compose de producción expone el par de configuración de rotación opcional.

## Producto y cálculos

- Retenciones globales iniciales: 2026 = 15,25 %; 2027 = 16,00 %. No se editan desde la API.
- Porcentajes del Dashboard: entero más cercano; las mitades se redondean alejándose de cero. Los porcentajes se redondean por separado y pueden no sumar visualmente 100 %.
- Los cambios de horas se guardan al presionar Enter o al perder el foco. La UI evita envíos duplicados, presenta estado de guardado y usa los totales confirmados por el servidor.
- Las escrituras de un período se serializan en backend y usan versión esperada por registro. Un cambio obsoleto devuelve 409; la UI conserva el borrador y permite reaplicarlo después de recargar.
- CLP se almacena como entero; el redondeo legal de retención es por institución con `MidpointRounding.AwayFromZero`.
- Para las liquidaciones privadas de Sanatorio Alemán, `TOTAL LIQUIDACIÓN` es el bruto autoritativo; el período contable y la tasa anual aplicada se determinan por `Fecha Liquidación`, conservando el período de servicio impreso por separado. La retención se calcula y redondea por PDF usando el snapshot del período contable. La suma de `Valor Pago` se valida y nunca se vuelve a aplicar el `Factor Pago` al total.
- Al introducir la contabilización por fecha de liquidación para Sanatorio Alemán, las importaciones existentes conservan el período mensual al que ya estaban asignadas; la migración no las reubica. Cada institución privada futura debe definir su propia regla de período contable.
- El parser privado inicial usa PdfPig sin OCR. Los servicios del formato de Sanatorio Alemán se procesan con la misma regla; se rechaza el PDF si el RUT pagador no está sembrado/configurado, el RUT cobrador no coincide o no cuadran el conteo/importes.
- Los PDF privados se limitan a 1 MiB y se guardan en un volumen Docker persistente del backend, sin respaldo fuera de Docker. El propietario puede eliminar su archivo y liquidación; propietario y administradores pueden descargarlo.
- Los importes expuestos como JSON se limitan al entero seguro de JavaScript (`9.007.199.254.740.991`) para que el navegador conserve exactitud; no limita la cantidad de filas ni las horas por períodos.
- El intervalo del Dashboard se limita técnicamente a 50 años por consulta; no limita los datos que se pueden registrar o conservar.
- El workspace carga hasta 100 filas recientes por institución y pagina las anteriores con cursor de orden. No establece un máximo funcional de registros.

## Pendientes de producto preservados

- El PRD deja por definir el reordenamiento manual y la restauración/deshacer de registros. La interfaz conserva el orden de inserción y confirma la eliminación; no se implementa reordenamiento manual ni papelera hasta definir esos comportamientos.
