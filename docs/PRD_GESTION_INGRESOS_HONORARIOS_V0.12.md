# PRD — Importación privada de Centro Cebien

**Versión:** 0.12 — borrador para validación
**Alcance:** Construcción de la liquidación privada de Centro Cebien
**Estado:** Definición funcional para el MVP

Este PRD define exclusivamente la incorporación de liquidaciones de **Centro Cebien** a Gestión de ingresos por honorarios. Se apoya en el contexto y las reglas compartidas del producto descritos en `PRD_GESTION_INGRESOS_HONORARIOS.md` y `PRD_GESTION_INGRESOS_HONORARIOS_V0.11.md`.

La versión 0.12 no reemplaza las reglas públicas ni el flujo privado de PDF de Sanatorio Alemán. El importador de Sanatorio Alemán conserva su comportamiento. Las reglas de Centro Cebien se implementan como una regla institucional independiente.

## 1. Resumen

Centro Cebien envía al profesional un correo mensual con el período de atención, prestaciones y cantidades, y el desglose de la boleta de honorarios. El sistema permitirá pegar el cuerpo del correo, analizarlo, elegir el Mes contable y registrar el tiempo por atención antes de confirmar.

Para el correo de ejemplo, el sistema debe extraer:

- Profesional: `DRA. ALEJANDRA PEZO`.
- Mes de atención del origen: agosto de 2026.
- Atenciones psiquiátricas: 19.
- Bruto: $418.000.
- Retención informada: 15,25 %, equivalente a $63.745.
- Líquido informado: $354.255.

El profesional elige manualmente el Mes contable, que puede ser distinto del mes de atención. El sistema aplica la tasa global anual configurada para el año del Mes contable, calcula los importes y los contrasta con el desglose del correo. Una discrepancia impide confirmar.

## 2. Objetivos

- Incorporar las liquidaciones mensuales de Centro Cebien sin transcribir manualmente el bruto, la retención y el líquido.
- Conservar el cuerpo original del correo y su hash para auditoría y detección de duplicados.
- Permitir que el profesional seleccione el período mensual contable.
- Calcular minutos y horas totales usando el tiempo por atención ingresado por el profesional.
- Consolidar los importes y métricas en el período contable seleccionado, en el módulo privado y en el dashboard.
- Validar que el correo corresponde al profesional autenticado.

## 3. Alcance

### Incluido

1. Agregar **Centro Cebien** al selector de institución de **Nueva liquidación privada**.
2. Permitir pegar el cuerpo del correo en un campo de texto.
3. Analizar el formato conocido de Cebien y mostrar una previsualización editable únicamente en sus campos de entrada autorizados: Mes contable y minutos por atención.
4. Conservar el mes de atención que aparece en el correo como período de servicio, separado del Mes contable.
5. Registrar la entidad pagadora `DR. BENJAMIN VICENTE PARADA Y CIA. LIMITADA`, RUT `76015783-K`.
6. Validar el nombre de profesional del correo contra el perfil autenticado.
7. Validar y calcular bruto, retención y líquido por correo/importación.
8. Persistir el cuerpo fuente, su hash y los campos extraídos; permitir la acción **Ver correo**.
9. Permitir más de una liquidación Cebien en un mismo mes contable cuando la clave de negocio sea distinta.
10. Representar como vacíos los datos que no aparecen en el correo, sin inventar valores.

### Fuera del alcance

- Conectarse a Gmail, Microsoft 365, IMAP u otra casilla para recuperar correos automáticamente.
- Procesar adjuntos PDF de Centro Cebien o utilizar OCR.
- Crear una fila de base de datos por paciente o guardar nombres de pacientes.
- Aplicar la regla de Centro Cebien a otras instituciones privadas.
- Cambiar el importador PDF de Sanatorio Alemán o las reglas del flujo público.

## 4. Usuarios y autorización

### Profesional

Puede importar, consultar y eliminar sus liquidaciones privadas. La propiedad se asigna en backend a partir del usuario autenticado; el cliente no puede elegir libremente el profesional propietario.

El correo no contiene RUT del cobrador. Para Centro Cebien, el backend normaliza el nombre indicado en `PROFESIONAL` y lo compara con el nombre del perfil autenticado. Para el ejemplo, `DRA. ALEJANDRA PEZO` coincide con un perfil cuyo nombre sea Alejandra Pezo después de normalizar tratamiento, tildes, puntuación, mayúsculas y espacios. Si no coincide, se rechaza la importación.

### Administrador

Puede consultar las liquidaciones de un profesional autorizado y ver su fuente de correo. No puede importar ni eliminar en su nombre.

## 5. Datos de Centro Cebien

### 5.1 Institución y pagador

- Nombre visible de la institución: **Centro Cebien**.
- Razón social: `DR. BENJAMIN VICENTE PARADA Y CIA. LIMITADA`.
- RUT normalizado: `76015783-K`.
- Regla inicial: `CENTRO_CEBIEN_EMAIL`, versión 1.

La institución, entidad pagadora y regla se configuran mediante seed/migración. Un pagador no configurado se rechaza; no se crea automáticamente a partir de texto libre.

### 5.2 Ejemplo de correo y extracción

El parser reconoce el cuerpo del correo con estos datos:

| Campo | Valor del ejemplo | Tratamiento |
|---|---|---|
| Centro | CENTRO CEBIEN | Identifica la institución seleccionada |
| Profesional | DRA. ALEJANDRA PEZO | Se coteja con el perfil autenticado |
| Mes de atención | Agosto de 2026 | Se conserva como período de servicio |
| Atenciones psiquiátricas | 19 | Se incorpora al conteo de atenciones |
| Atenciones psicológicas | Sin cantidad | No incrementa el conteo |
| Test Rorschoch, Bender y Wais | Sin cantidad | No incrementan el conteo |
| Atenciones efectuadas mes | $418.000 | Bruto autoritativo de la fuente |
| Tasa informada | 15,25 % | Se compara con la tasa configurada |
| Retención informada | $63.745 | Se compara con el cálculo del backend |
| Líquido informado | $354.255 | Se compara con bruto menos retención |

En este formato, `418,000` representa $418.000 CLP. Se normaliza como un monto entero según la convención del correo. Un importe ambiguo que no se pueda interpretar de forma segura rechaza el análisis.

La firma `Angelica Escalona Sanhueza / Colegio Contadores` no se interpreta como profesional ni ejecutor. Se conserva únicamente dentro del cuerpo original.

## 6. Períodos y cálculo

### 6.1 Período de atención y Mes contable

- `MES DE ATENCIÓN` extraído del correo es el período de servicio del origen.
- El profesional debe elegir manualmente el **Mes contable** con un selector de mes. El campo es obligatorio y no se preselecciona usando `MES DE ATENCIÓN`.
- El período de servicio y el Mes contable se muestran por separado en la previsualización y se conservan en el registro.
- El período contable manual determina la asociación al workspace mensual y al dashboard.
- El año del Mes contable determina la tasa global anual utilizada en los cálculos.

### 6.2 Importes y retención

El bruto procede de `Atenciones efectuadas mes`, no de un total editable por el usuario. Para cada liquidación Cebien:

```text
Bruto = monto leído de Atenciones efectuadas mes
Retención = redondear(Bruto × tasa global anual del año contable / 100)
Líquido = Bruto − Retención
```

- La tasa global anual se obtiene de la configuración del sistema y no se edita desde el API ni desde la interfaz.
- La retención se calcula por liquidación con redondeo `AwayFromZero`.
- La tasa, retención y líquido del correo son controles de consistencia; no sustituyen los cálculos del backend.
- Si la tasa o los importes del correo no coinciden con los cálculos del backend, la importación no se confirma.
- Para agosto de 2026, con tasa de 15,25 %, $418.000 producen $63.745 de retención y $354.255 de líquido.

### 6.3 Atenciones y duración

- Se suman las cantidades numéricas informadas en las filas de prestaciones del correo. Las filas vacías no cuentan.
- El ejemplo contiene 19 atenciones psiquiátricas.
- El profesional ingresa un único valor de minutos enteros por atención, mayor que cero. Se aplica uniformemente a todas las atenciones de la liquidación.
- Minutos totales = atenciones × minutos por atención.
- No se generan filas individuales por paciente ni se calcula duración promedio.

## 7. Flujo de importación

1. El profesional pulsa **Nueva liquidación privada** y selecciona **Centro Cebien**.
2. Pega el cuerpo del correo, elige el Mes contable manualmente e ingresa los minutos por atención.
3. Pulsa **Analizar**. El backend normaliza y analiza el texto, valida el formato, identifica al profesional, calcula los importes y comprueba el hash/duplicado.
4. La previsualización muestra el período de atención del correo, el Mes contable seleccionado, el profesional cotejado, prestaciones y cantidades, minutos, bruto, tasa, retención y líquido.
5. El usuario confirma. La creación de la liquidación y actualización de agregados se realiza transaccionalmente.
6. Un error de formato, identidad, duplicado o importes conserva el formulario y bloquea la confirmación.

El acceso a Centro Cebien se diferencia del flujo Sanatorio Alemán en el selector de institución; no reemplaza ni modifica el flujo de PDF existente.

## 8. Persistencia y campos vacíos

Cada registro conserva:

- Profesional propietario, institución, entidad pagadora y regla Cebien.
- Fuente de tipo `EmailBody`, cuerpo original y hash.
- Nombre de profesional informado y resultado del cotejo con el perfil.
- Período de atención, Mes contable y prestaciones/cantidades reconocidas.
- Minutos por atención, minutos totales, bruto, tasa aplicada, retención y líquido.

Los siguientes campos quedan nulos si el correo no los contiene: número de liquidación, fecha exacta, quincena, RUT del cobrador, ejecutor, total de servicio independiente y metadatos/ruta de PDF. La interfaz los presenta como `—`; no se deriva una fecha desde el mes ni se crean números ficticios.

El cuerpo del correo original se conserva junto al hash para auditoría y consulta autorizada mediante **Ver correo**. No se genera un PDF sustituto.

## 9. Duplicados y correcciones

Una liquidación se considera duplicada si coincide el hash del cuerpo normalizado o la clave de negocio Cebien:

```text
profesional + entidad pagadora + Mes contable + período de atención
+ bruto + cantidades por prestación
```

La clave no reserva el mes completo: permite varias liquidaciones para el mismo profesional y Mes contable cuando difieren en los datos de la clave. Si se necesita corregir una liquidación confirmada, el propietario elimina el registro y vuelve a importar el correo corregido.

## 10. Workspace y dashboard

El workspace `/month` permanece destinado al trabajo por hora y no incluye totales privados en su respuesta ni interfaz. Las liquidaciones Cebien se muestran en el módulo privado, filtradas por Mes contable.

Los agregados de Centro Cebien se incorporan al período contable seleccionado y a la columna privada correspondiente del dashboard. El líquido combinado suma los montos públicos y privados del mismo período, sin mezclar sus métricas.

Las reglas generales del dashboard se mantienen: columnas institucionales dinámicas con líquido positivo, alias públicos canónicos, total combinado independiente de filtros y ausencia de columnas por modalidad.

## 11. Seguridad, fuentes y eliminación

- El backend obtiene siempre el propietario a partir del usuario autenticado.
- La validación de nombre se realiza en backend; un nombre enviado por el navegador no determina al propietario.
- El pagador se reconoce por su entidad configurada y RUT normalizado.
- Solo el propietario elimina su liquidación. Administradores autorizados pueden consultar y ver el correo.
- Al eliminar un registro, se elimina también el cuerpo fuente y se recalculan los agregados del Mes contable en una transacción.
- El cuerpo del correo no se expone mediante una URL pública; se consulta mediante una operación autorizada.
- No se guardan nombres de pacientes ni información de cada atención individual.

## 12. Requisitos funcionales de Centro Cebien

- **CEB-RF-01:** Ofrecer Centro Cebien como institución en el flujo **Nueva liquidación privada** sin cambiar el flujo Sanatorio Alemán.
- **CEB-RF-02:** Permitir pegar el cuerpo del correo y analizarlo en backend.
- **CEB-RF-03:** Requerir un Mes contable seleccionado manualmente, independiente de `MES DE ATENCIÓN`.
- **CEB-RF-04:** Extraer el nombre del profesional, período de atención, cantidades por prestación, bruto y desglose de boleta.
- **CEB-RF-05:** Validar que el nombre normalizado del correo coincide con el perfil autenticado.
- **CEB-RF-06:** Aplicar la tasa global anual del año contable, calcular retención/líquido y contrastarlos con el correo.
- **CEB-RF-07:** Permitir ingresar minutos enteros positivos por atención y aplicarlos uniformemente a las atenciones contadas.
- **CEB-RF-08:** Rechazar correos con estructura no reconocida, nombre no coincidente, pagador no configurado, falta de tasa, duplicado o importes inconsistentes.
- **CEB-RF-09:** Persistir el cuerpo original, su hash y los campos ausentes como nulos.
- **CEB-RF-10:** Permitir visualizar el cuerpo original mediante una acción autorizada **Ver correo**.
- **CEB-RF-11:** Detectar duplicados por hash y por la clave de negocio Cebien, permitiendo varias liquidaciones distintas en el mismo Mes contable.
- **CEB-RF-12:** Actualizar agregados mensuales y dashboard en la misma transacción que crea/elimina la liquidación.
- **CEB-RF-13:** Mantener Sanatorio Alemán y el flujo público sin cambios funcionales.

## 13. Criterios de aceptación

### CEB-CA-01 — Selector de institución

Al pulsar **Nueva liquidación privada**, la persona puede elegir Sanatorio Alemán o Centro Cebien. La elección Cebien abre el formulario de correo; la elección Sanatorio Alemán mantiene el formulario de PDF.

### CEB-CA-02 — Analizar el correo de ejemplo

Al pegar el ejemplo, el análisis reconoce `DRA. ALEJANDRA PEZO`, período de atención agosto de 2026, 19 atenciones psiquiátricas y $418.000 de bruto. Las prestaciones sin cantidades no suman atenciones.

### CEB-CA-03 — Cotejar profesional

El correo se acepta si el nombre normalizado coincide con el perfil autenticado y se rechaza si no coincide. La propiedad del registro siempre corresponde al usuario autenticado.

### CEB-CA-04 — Mes contable manual

El campo Mes contable es obligatorio y no se inicializa automáticamente con agosto de 2026 ni con otro valor del cuerpo. El correo muestra período de atención y la previsualización conserva aparte el mes contable seleccionado, aunque difieran.

### CEB-CA-05 — Aplicar tiempo ingresado

Si el profesional ingresa `N` minutos por atención, el total se calcula como `19 × N`. Se rechazan cero, negativos y fracciones; el valor uniforme se aplica a todas las atenciones contadas.

### CEB-CA-06 — Calcular desglose

Con Mes contable en 2026 y tasa de 15,25 %, $418.000 producen $63.745 de retención y $354.255 líquidos. Si los valores informados en el correo no coinciden con los cálculos, no se confirma.

### CEB-CA-07 — Campo ausente

Número de liquidación, fecha exacta, quincena, RUT del cobrador, ejecutor y metadatos de PDF quedan nulos y se muestran como `—`. El nombre de la persona que firma como contadora no se interpreta como profesional ni ejecutor.

### CEB-CA-08 — Preservar y consultar la fuente

El cuerpo original y su hash quedan vinculados al registro. El propietario y administradores autorizados pueden usar **Ver correo**; otro profesional no puede consultar el texto.

### CEB-CA-09 — Duplicados y varias liquidaciones en el mes

El mismo cuerpo/clave de negocio se bloquea. Otra liquidación del mismo profesional y Mes contable se admite cuando su clave de negocio sea distinta. Eliminar una liquidación recalcula los agregados del período.

### CEB-CA-10 — Integrar con los agregados

Los importes de la liquidación se suman al Mes contable seleccionado en el módulo privado y dashboard. El total combinado mensual incluye el líquido privado sin modificar los totales públicos.

### CEB-CA-11 — Mantener flujos existentes

Las pruebas del importador PDF de Sanatorio Alemán y el workspace público continúan pasando sin cambio funcional.

## 14. Decisiones pendientes

- **Por definir:** límite técnico máximo para el cuerpo del correo de Centro Cebien.
- La lectura automática de casillas de correo, OCR y formatos de otras instituciones privadas quedan fuera de esta versión.
