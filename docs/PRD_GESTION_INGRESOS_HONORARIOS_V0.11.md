# PRD — Gestión de ingresos por honorarios para profesionales de la salud

**Versión:** 0.11 — borrador revisado  
**Alcance:** Gestión de ingresos públicos y privados  
**Estado:** Extensión del MVP actual

Este documento amplía el alcance público del PRD anterior. Las reglas públicas que no se contradicen con esta versión siguen vigentes; para las liquidaciones privadas de Sanatorio Alemán, esta versión es la referencia funcional.

## 1. Resumen del producto

La aplicación permite a profesionales de la salud registrar sus ingresos por servicios realizados para instituciones públicas y privadas, analizar su evolución mensual y consultar un dashboard consolidado.

El sistema mantiene dos modelos de cálculo diferenciados:

- **Instituciones públicas:** el profesional registra horas; el sistema calcula bruto, retención y líquido estimado usando su tarifa horaria anual.
- **Instituciones privadas:** el profesional importa una liquidación PDF; el sistema conserva cada documento por separado, obtiene su monto bruto y cantidad de atenciones, registra los minutos por atención y calcula retención y líquido.

Ambos modelos pueden contribuir al resumen mensual de un profesional. El dashboard presenta sus métricas por modalidad y también el líquido mensual combinado.

## 2. Problema y objetivos

Los profesionales pueden recibir ingresos de instituciones con reglas distintas. Para las instituciones públicas, el sistema actual calcula honorarios a partir de horas y tarifas. Algunas instituciones privadas, como Sanatorio Alemán, entregan liquidaciones PDF con el monto bruto ya calculado según sus propias reglas de participación.

El profesional necesita:

- Incorporar liquidaciones privadas sin transcribir manualmente sus totales.
- Conservar cada PDF y sus datos principales de manera independiente.
- Conocer bruto, retención y líquido por documento, institución y mes.
- Registrar el tiempo por atención y analizar cantidad de atenciones y tiempo total.
- Comparar la evolución mensual de ingresos públicos y privados, además del total líquido combinado.

## 3. Usuarios y autorización

### Profesional

Puede consultar y administrar sus propios datos públicos y privados, incluida la carga y eliminación de sus liquidaciones PDF. Su perfil incluye un RUT chileno normalizado, validado y único. No puede acceder a la información de otros profesionales.

Los perfiles existentes pueden no tener RUT al aplicar la migración. El profesional debe completar un RUT válido en su perfil antes de importar una liquidación privada.

### Administrador

Puede consultar información de profesionales autorizados según las reglas existentes del sistema. La propiedad de una importación se determina en backend a partir del usuario autenticado, no de un profesional indicado libremente por el cliente.

Las autorizaciones se validan siempre en backend.

## 4. Alcance

### Incluido

1. Se conserva el flujo público actual:
   - Asociar instituciones públicas.
   - Configurar tarifas horarias anuales personales.
   - Registrar horas mensuales.
   - Calcular bruto, retención y líquido estimado.
2. Se agrega importación de liquidaciones privadas en PDF.
3. La primera regla privada corresponde a la plantilla **“Liquidación por Participaciones” de Sanatorio Alemán**.
4. Cada PDF genera un registro independiente, aunque existan otros documentos para la misma institución, mes o quincena.
5. El dashboard muestra los resultados públicos y privados por separado y el líquido mensual combinado.
6. El RUT del cobrador leído desde el PDF debe coincidir con el RUT del perfil profesional antes de confirmar la importación.
7. El parser inicial usa PdfPig para extraer texto del PDF. No se incluye OCR en esta versión.
8. Cada liquidación conserva el PDF original en un volumen persistente de Docker montado en el contenedor del backend; la base de datos conserva su referencia, hash y datos de importación.
9. El profesional propietario y los administradores pueden descargar el PDF mediante una operación autorizada. Solo el profesional propietario puede eliminarlo.
10. El archivo PDF cargado no puede superar 1 MB (1.048.576 bytes).

Esta versión implementa únicamente Sanatorio Alemán. Los RUT pagadores reconocidos para esta institución son `76389986-1` y `88611600-4`. Las reglas de nuevas instituciones privadas se agregarán posteriormente con sus propios formatos y reglas de negocio.

### Fuera del alcance inicial

- Emisión automática de boletas o facturas.
- Confirmación de transferencia bancaria o seguimiento de pagos efectivamente recibidos.
- Liquidaciones privadas de instituciones distintas de Sanatorio Alemán hasta que se definan sus reglas.
- Registrar una fila propia en la base de datos por cada paciente o atención.
- Identificar y contar pacientes únicos mediante deduplicación de nombres.
- Reglas privadas que aún no hayan sido documentadas con ejemplos y criterios de cálculo.
- Procesar PDFs escaneados sin capa de texto mediante OCR.

## 5. Conceptos del dominio

### 5.1 Período mensual

Espacio de trabajo identificado por profesional, año y mes. Existe una sola fila mensual por profesional y período; puede reunir actividad pública, liquidaciones privadas o ambas.

### 5.2 Institución pública

Organización del sistema público relacionada con un profesional. La relación profesional–institución define las tarifas horarias anuales aplicables al cálculo público.

### 5.3 Institución privada

Organización privada para la que el profesional presta servicios. En la primera implementación se registra Sanatorio Alemán como una institución.

### 5.4 Entidad pagadora

Razón social y RUT que aparecen como pagador en el PDF. Una misma institución privada puede tener varias entidades pagadoras. Los documentos entregados contienen dos RUT pagadores diferentes, `76389986-1` y `88611600-4`, que se conservan como entidades separadas bajo Sanatorio Alemán. La institución se determina por el RUT pagador reconocido, no solo por el nombre impreso.

### 5.5 Regla privada de liquidación

Regla asociada a una institución y a un formato de liquidación. La primera regla es **Liquidación por Participaciones — Sanatorio Alemán**. Cada documento conserva la regla y versión utilizadas para procesarlo.

### 5.6 Liquidación privada

Registro asociado a un PDF, un profesional, una entidad pagadora y un período mensual contable. Conserva por separado el período de servicio impreso en el PDF y el período contable definido por las reglas de la institución. Guarda el PDF original y los datos extraídos o ingresados para ese documento. El RUT del cobrador extraído se valida contra el perfil profesional. Solo se crea el registro confirmado después de superar todas las validaciones; un PDF rechazado no deja un registro pendiente ni guarda el original.

### 5.7 Atención

Fila de atención identificada en el detalle del PDF. Para este MVP, la cantidad de atenciones corresponde al número de filas, según la solicitud funcional. No es un conteo de personas únicas.

## 6. Reglas de cálculo

### 6.1 Ingresos públicos

Se mantienen las reglas vigentes:

```text
Horas totales por institución = suma de registros de horas
Bruto público = horas totales × tarifa horaria anual
Retención pública = redondeo de la retención calculada por institución
Líquido público = bruto público − retención pública
```

La tasa de retención se obtiene de la configuración anual global. Cada período conserva el snapshot aplicado. Los períodos existentes no se recalculan silenciosamente.

### 6.2 Ingresos privados

En el PDF, `Total Liquidación` representa el **monto bruto del profesional**. El `Factor Pago` ya se refleja en los importes `Valor Pago` de las atenciones; el sistema no vuelve a aplicar ese factor sobre el total del documento.

El parser lee `Total Liquidación` como el bruto oficial del documento. De manera independiente, el procesamiento cuenta las filas de atención y suma sus importes `Valor Pago` para validar el monto leído. El parser no calcula nuevamente la participación del centro a partir de `Valor Venta` y `Factor Pago`.

Para cada PDF:

```text
Bruto privado del PDF = Total Liquidación del PDF
Retención privada del PDF =
    redondear(Bruto privado del PDF × tasa anual aplicada / 100)
Líquido privado del PDF = Bruto privado del PDF − Retención privada del PDF
```

- Para Sanatorio Alemán, el período contable corresponde al año/mes de `Fecha Liquidación`; se usa la tasa global anual vigente para ese año contable.
- La retención se calcula y redondea **por PDF**, no después de sumar las liquidaciones del mes.
- Se utiliza el redondeo *midpoint away from zero* (`AwayFromZero`).
- El período conserva la tasa aplicada como snapshot, de modo que las liquidaciones históricas no cambien si posteriormente se corrige la tasa global.
- Si no existe una tasa configurada para el año de la fecha de liquidación, el sistema no puede confirmar el cálculo de líquido y debe informar que falta la configuración.

### 6.3 Atenciones y tiempo

- Cantidad de atenciones por PDF = número de filas de atención leídas.
- En la carga, el profesional ingresa los minutos de una atención para ese PDF como un número entero mayor que cero; no se establece un máximo funcional.
- Ese valor se aplica a todas las atenciones del PDF, sea cual sea el servicio indicado en la plantilla.
- Minutos totales del PDF = cantidad de atenciones × minutos por atención.
- El resumen mensual suma las atenciones y minutos de todos los documentos del mes.
- No se calcula ni presenta un promedio mensual de duración.

La interfaz puede describir la métrica como **“Atenciones”** o **“Pacientes atendidos según filas de liquidación”**, dejando claro que no representa pacientes únicos.

### 6.4 Consolidación mensual

Los totales públicos existentes conservan su significado público. Se agregan totales privados independientes:

```text
Bruto privado mensual = suma de brutos de los PDF
Retención privada mensual = suma de retenciones calculadas por PDF
Líquido privado mensual = suma de líquidos de los PDF
```

El dashboard calcula el total líquido combinado así:

```text
Líquido mensual combinado =
    líquido público estimado + líquido privado calculado
```

El dashboard también mantiene el desglose de cada modalidad. No se sobrescriben los totales públicos con montos privados.

## 7. Importación de PDF de Sanatorio Alemán

### 7.1 Datos identificados

El importador procesa la plantilla “Liquidación por Participaciones” y obtiene los siguientes datos por PDF. La misma regla se aplica a todos los servicios incluidos en esta plantilla; no se aplican fórmulas distintas según el nombre de la prestación o servicio.

- Entidad pagadora, razón social y RUT.
- Cobrador y su RUT.
- Número y fecha de liquidación.
- Período del servicio, año, mes y quincena, conservados tal como aparecen en el PDF.
- Período contable, año y mes, determinados por `Fecha Liquidación` y usados para asociar la liquidación al workspace mensual y al dashboard.
- Servicio de pago y ejecutor.
- Total del servicio, cuando aparece, y total de la liquidación.
- Cantidad de atenciones contadas desde las filas identificadas por un N.º de atención válido.
- Cantidad de cancelaciones indicada en el cierre, si aparece, para contrastarla con el conteo de filas.

El período del PDF y el mes contable se presentan por separado en la previsualización. El profesional ingresa los minutos enteros por atención durante la carga. El valor debe ser mayor que cero y se aplica uniformemente a todas las atenciones del PDF.

El tamaño máximo del archivo cargado es 1 MB (1.048.576 bytes). Los PDF que superen ese límite se rechazan antes de procesarse.

PdfPig extrae el texto y sus posiciones. La lógica de aplicación interpreta la estructura y las columnas del formato conocido, incluyendo las filas que continúan en otra página. Para calcular las atenciones no se cuentan líneas físicas de texto: se cuentan los registros que contienen un identificador N.º de atención válido. Para validar el bruto se suman los valores `Valor Pago` leídos de esas filas.

### 7.2 Persistencia

Por cada PDF, el sistema conserva:

- El PDF original asociado a su liquidación.
- RUT del cobrador leído del documento y resultado de su comparación con el perfil.
- Número y fecha de liquidación, período de servicio/quincena, período contable, servicio, ejecutor, entidad pagadora y RUT.
- Total del servicio, si aparece, y `Total Liquidación` como monto bruto autoritativo.
- El bruto, la tasa aplicada, la retención y el líquido calculados.
- La cantidad de atenciones, la cantidad informada en el PDF cuando existe, los minutos por atención y los minutos totales calculados.
- Hash del archivo y clave de negocio para reconocer una carga duplicada o conflictiva.

El sistema no conserva filas estructuradas individuales de pacientes ni sus nombres como registros separados. Puede leer temporalmente el detalle para contar filas y validar importes.

El archivo se conserva en un volumen persistente de Docker montado en el contenedor del backend. La base de datos guarda la referencia al archivo y su hash; el archivo no depende del filesystem efímero de la imagen o del contenedor. No se contempla una copia de respaldo fuera de Docker en esta versión; perder o eliminar el volumen implica perder los PDF originales.

El profesional propietario y los administradores pueden descargar el PDF. La descarga requiere autorización del backend según rol y propiedad; el archivo no se expone mediante una URL pública. Solo el profesional propietario puede eliminar la liquidación. Al eliminarla, se elimina también el archivo original y sus datos persistidos, y se recalculan los agregados del período.

### 7.3 Validación

Antes de confirmar una liquidación, el sistema:

- Comprueba que reconoce el formato.
- Valida formato y dígito verificador del RUT del cobrador y exige coincidencia normalizada con el RUT del profesional autenticado.
- Reconoce el RUT pagador mediante el catálogo de pagadores permitidos de Sanatorio Alemán. Un RUT desconocido rechaza la importación; no crea una institución ni guarda una carga pendiente. El pagador se agrega mediante migración/seed y luego se vuelve a cargar el PDF.
- Compara la cantidad de filas con el conteo indicado en el cierre del PDF, cuando está disponible.
- Usa `Total Liquidación` como bruto oficial; compara el valor con la suma de `Valor Pago`. Si aparecen uno o más subtotales `Total Servicio`, suma los subtotales y compara el resultado con `Total Liquidación`.
- Bloquea tanto un hash ya importado como una clave de negocio duplicada basada en profesional, RUT pagador, número de liquidación y año/mes/quincena del período de servicio del PDF.
- Presenta los valores interpretados, el período del PDF, el período contable y los minutos ingresados antes de confirmar. Los campos extraídos no se editan manualmente; el tiempo por atención es el único dato del análisis ingresado por el usuario.

Si el PDF no se puede interpretar, no contiene texto extraíble, el RUT cobrador no coincide, el pagador no está reconocido o el conteo/suma no coincide con los valores de cierre, el sistema informa el problema y no confirma la importación. No se ejecuta OCR ni se permite corregir manualmente el conteo o los importes; el profesional debe resolver la fuente y volver a cargarla. Si ya existe una carga conflictiva, el profesional puede eliminarla y luego importar el PDF corregido.

## 8. Modelo conceptual de datos

```text
Profesional
└── Período mensual (profesional + año + mes)
    ├── Períodos de institución pública
    │   └── Registros de horas
    └── Liquidaciones privadas
        ├── Institución privada
        ├── Entidad pagadora/RUT
        └── Regla privada aplicada
```

### Entidades actuales que se conservan

- `MonthlyPeriod` / `periodos_mensuales`: una fila por profesional y mes.
- `Professional`: se amplía con el RUT profesional normalizado. El campo puede quedar vacío en perfiles existentes, pero debe estar completado para importar liquidaciones privadas.
- `PeriodInstitution`: datos de una institución pública dentro del mes, con la tarifa anual aplicada.
- `HourRecord`: registros de horas públicas.

`PeriodInstitution` y `HourRecord` continúan representando exclusivamente el flujo público.

### Entidades nuevas

- **Institución privada:** catálogo de organizaciones privadas.
- **Entidad pagadora privada:** razón social y RUT asociados a una institución privada.
- **Regla privada:** código y versión del formato o regla de liquidación.
- **Liquidación privada:** una fila por PDF, vinculada al período mensual, institución, pagador y regla.

La liquidación privada guarda, como mínimo, el RUT del cobrador extraído, el número y fecha de liquidación, el período de servicio/quincena, el período contable, el servicio, la entidad pagadora, el total bruto, la retención y el líquido por PDF, el número de atenciones, los minutos por atención, los minutos totales y la referencia al PDF original. La tasa aplicada se toma del snapshot del período contable; no se mantiene una tasa distinta por documento.

### Extensión del período mensual

A `periodos_mensuales` se agregan métricas privadas independientes:

- Bruto privado.
- Retención privada.
- Líquido privado.
- Cantidad de atenciones privadas.
- Minutos totales de atención privada.

Los campos actuales de horas, bruto, retención y líquido públicos no cambian de significado. La liquidación individual queda en su propia entidad; la fila mensual funciona como resumen, no como almacenamiento de PDFs.

Los agregados privados del período son la suma de sus liquidaciones confirmadas. El bruto, la retención y el líquido privados se suman desde los valores guardados por PDF; no se vuelve a calcular ni redondear una sola retención sobre el total mensual. El total combinado del dashboard se obtiene sumando el líquido público y el privado.

## 9. Workspace mensual

El workspace público mantiene el comportamiento actual: el profesional selecciona instituciones participantes y edita horas libremente.

El período mensual puede además contener liquidaciones privadas importadas. En Sanatorio Alemán, las liquidaciones se vinculan al mes contable derivado de `Fecha Liquidación`, conservando la quincena y el período de servicio impresos en el PDF. Una liquidación privada no crea registros de horas ni tarifas horarias. Las métricas de atenciones y duración corresponden únicamente a las instituciones privadas; no se combinan con las horas públicas.

La confirmación de una liquidación y la actualización de sus agregados privados deben ocurrir en una sola transacción. La eliminación por el profesional propietario borra el registro y el PDF, y actualiza los agregados del mes en una sola operación. El total mensual que muestra el servidor es autoritativo.

## 10. Dashboard

### Filtros

- Intervalo de años.
- Institución pública o privada.
- Profesional para usuarios administradores, sin combinar los datos de distintas personas.

### Tabla mensual

La tabla contiene año, mes, una columna por cada institución con líquido mayor que cero en al menos un mes del intervalo y una columna de `Líquido combinado`. No muestra grupos de columnas `Ingresos públicos` o `Ingresos privados`, ni columnas de horas, bruto, retención, atenciones o minutos.

Las instituciones públicas se identifican por el nombre normalizado del catálogo. `SAPU Lorenzo Arenas` y `Lorenzo Arenas` se agrupan bajo la etiqueta **SAPU Lorenzo Arenas**; `SAR Tucapel` y `Tucapel` se agrupan bajo **SAR TUCAPEL**. Todas las demás instituciones públicas que tengan líquido positivo también se muestran con su nombre de catálogo.

Sanatorio Alemán aparece como **una sola columna** de tipo `Privada` y su líquido mensual suma las liquidaciones de sus entidades pagadoras, aunque tengan RUT distintos. El detalle permite consultar cada PDF y su pagador. Las demás instituciones privadas con líquido positivo aparecen como columnas independientes.

Cada columna por institución muestra el líquido y su participación sobre el líquido combinado mensual: `líquido de la institución / líquido combinado del mes × 100`. El porcentaje se redondea al entero más cercano, con mitades alejándose de cero; los porcentajes se redondean por separado y pueden no sumar visualmente 100 %. Si el total líquido combinado es cero, la participación se presenta como `—`.

Las instituciones con líquido cero en todos los meses del intervalo no generan una columna. En un mes con período, si una institución no registró ingreso, su celda muestra cero; si no hay período, el mes se muestra sin dato. Las atenciones y minutos se consultan en el workspace privado y en el detalle de cada PDF, no en esta tabla.

El líquido combinado suma todas las instituciones públicas y privadas, sin importar si el usuario filtra qué columnas se muestran. El filtro solo modifica las columnas visibles y las series del gráfico.

### Gráfico

El gráfico presenta una serie líquida por cada institución con ingresos en el intervalo y una serie del líquido mensual combinado. No presenta series agrupadas por modalidad pública o privada.

Un mes sin información de ninguna modalidad aparece como un período sin datos. Si hay información de una modalidad, la otra aporta cero al total combinado de ese mes.

## 11. Historias de usuario

- Como profesional, quiero importar una liquidación privada en PDF y revisar los datos extraídos antes de confirmarla.
- Como profesional, quiero registrar mi RUT en el perfil para validar que una liquidación PDF me corresponde.
- Como profesional, quiero conservar cada liquidación por separado aunque varias pertenezcan al mismo mes.
- Como profesional, quiero eliminar una liquidación equivocada para borrar su PDF y corregir el período mediante una nueva carga.
- Como profesional o administrador autorizado, quiero descargar el PDF original de una liquidación.
- Como profesional, quiero ingresar los minutos por atención del PDF y consultar el tiempo total mensual.
- Como profesional, quiero ver el bruto, la retención y el líquido por liquidación y por mes.
- Como profesional, quiero comparar mis ingresos públicos y privados mes a mes, con un líquido mensual combinado.
- Como profesional, quiero ver una columna de líquido por cada institución con ingresos en el intervalo, sin agrupaciones de métricas por modalidad.
- Como profesional, quiero que una actualización de la tasa anual no modifique los cálculos de mis liquidaciones históricas.
- Como administrador, quiero consultar los resúmenes de un profesional sin modificar sus registros.

## 12. Requisitos funcionales

- **RF-01:** Mantener las instituciones públicas, relaciones, tarifas, períodos y horas actuales.
- **RF-02:** Permitir cargar un PDF de liquidación privada de un profesional autenticado.
- **RF-03:** Conservar el PDF original vinculado a una liquidación independiente.
- **RF-04:** Identificar la institución privada y la entidad pagadora mediante razón social y RUT.
- **RF-05:** Extraer número, fecha, período, quincena, servicio, totales y cantidad de filas de atención.
- **RF-06:** Permitir ingresar los minutos por atención para cada PDF.
- **RF-07:** Tratar `Total Liquidación` como bruto y calcular retención y líquido por PDF.
- **RF-08:** Usar para Sanatorio Alemán la tasa anual global del año de `Fecha Liquidación` y conservar el snapshot aplicado.
- **RF-09:** Rechazar o señalar errores de interpretación y cargas duplicadas.
- **RF-10:** Actualizar los agregados privados del mes contable derivado de `Fecha Liquidación` junto con la liquidación importada.
- **RF-11:** Mostrar en el dashboard mensual una columna de líquido por institución con datos y un líquido combinado, sin columnas agrupadas por modalidad.
- **RF-12:** Validar propiedad y permisos en backend para cada lectura y escritura.
- **RF-13:** Incorporar el RUT al perfil profesional, validarlo y normalizarlo; exigirlo antes de permitir la importación de PDF.
- **RF-14:** Comparar el RUT del cobrador extraído con el RUT del profesional autenticado y rechazar la liquidación si no coinciden.
- **RF-15:** Usar PdfPig para el formato inicial de Sanatorio Alemán y detectar PDFs sin texto extraíble; no ejecutar OCR en esta versión.
- **RF-16:** Conservar el PDF original en almacenamiento persistente del contenedor Docker del backend y guardar su referencia y hash en la liquidación.
- **RF-17:** Identificar filas válidas de atención mediante su N.º de atención; contar esas filas y sumar sus valores `Valor Pago` para validar el bruto leído.
- **RF-18:** Tratar `Total Liquidación` como el bruto autoritativo y compararlo con la suma de `Valor Pago` y con `Total Servicio` o sus subtotales cuando existan. Una discrepancia bloquea la confirmación.
- **RF-19:** Reconocer Sanatorio Alemán por los RUT pagadores configurados. Un RUT desconocido rechaza la importación; no se crea una institución ni se guarda una carga pendiente. El pagador se agrega mediante migración/seed y luego se vuelve a cargar el PDF.
- **RF-20:** Detectar duplicados por hash y por clave de negocio basada en período de servicio (profesional, RUT pagador, número de liquidación y año/mes/quincena del PDF). El listado y sus filtros usan el período contable. Un conflicto bloquea la nueva carga hasta que el propietario elimine la anterior.
- **RF-21:** Permitir que solo el profesional propietario elimine su liquidación; borrar el archivo original y los datos asociados y actualizar los agregados mensuales transaccionalmente.
- **RF-22:** Permitir descargar el PDF solo al profesional propietario y a administradores autorizados, mediante validación de rol y propiedad en backend.
- **RF-23:** Mostrar Sanatorio Alemán como una sola columna que suma sus pagadores y mapear los alias públicos acordados a sus etiquetas canónicas.
- **RF-24:** Rechazar archivos PDF cuyo tamaño supere 1 MB (1.048.576 bytes).
- **RF-25:** Aceptar para los minutos por atención únicamente valores enteros mayores que cero y aplicarlos a todas las atenciones del PDF.
- **RF-26:** Aplicar la misma regla de lectura y cálculo a todos los servicios presentes en la plantilla de Sanatorio Alemán.
- **RF-27:** Mostrar atenciones y minutos por PDF y como total mensual, sin calcular ni mostrar duración promedio.
- **RF-28:** Presentar la selección del PDF, los minutos por atención y la previsualización dentro de un modal antes de confirmar la importación.
- **RF-29:** Para Sanatorio Alemán, conservar el período de servicio del PDF y asignar las nuevas importaciones al período contable de su fecha de liquidación.

## 13. Criterios de aceptación

### CA-01 — Mantener cálculo público

Agregar, modificar o eliminar horas públicas sigue recalculando bruto, retención y líquido según las reglas actuales, sin incluir importes privados en esos campos.

### CA-02 — Importar cada PDF independientemente

Los documentos entregados se almacenan como dos liquidaciones separadas, aunque pertenezcan a Sanatorio Alemán y al mismo mes. Se conserva la razón social y el RUT pagador correspondiente a cada uno.

### CA-03 — Contar atenciones

El primer PDF produce 43 atenciones y el segundo 4, según sus filas. El sistema suma 47 atenciones en el resumen del mes. El conteo no deduplica personas.

### CA-04 — Calcular líquido por PDF

Con una tasa de 2026 de 15,25 % y redondeo `AwayFromZero`:

| Documento | Bruto | Retención | Líquido |
|---|---:|---:|---:|
| Primera liquidación | $616.988 | $94.091 | $522.897 |
| Segunda liquidación | $73.460 | $11.203 | $62.257 |

La retención se redondea para cada PDF antes de consolidar el mes.

### CA-05 — Consolidar el mes

Si las fechas de liquidación de los dos documentos anteriores pertenecen al mismo mes contable, el resumen privado de ese mes contiene:

- Bruto: $690.448.
- Retención: $105.294.
- Líquido: $585.154.
- Atenciones: 47.
- Minutos totales: la suma de las atenciones de cada PDF multiplicadas por sus respectivos minutos ingresados por atención.

### CA-06 — Calcular el total combinado

Si el líquido público del mes es `X`, el dashboard muestra un líquido mensual combinado de `X + $585.154`, manteniendo visible el desglose público y privado.

### CA-07 — Evitar duplicados

Volver a cargar el mismo PDF no incrementa nuevamente bruto, retención, líquido, atenciones ni minutos del mes.

### CA-08 — Mantener histórico

Una liquidación ya importada conserva la tasa y los valores calculados. Una corrección posterior de la tasa anual no cambia sus resultados existentes.

### CA-09 — Aislamiento por profesional

Un profesional solo puede consultar e importar información propia. El backend rechaza consultas o mutaciones que intenten acceder a liquidaciones de otro profesional.

### CA-10 — RUT profesional

Un perfil existente sin RUT puede seguir utilizándose para el flujo público. Antes de importar un PDF, el profesional debe ingresar un RUT válido en su perfil. La importación se confirma únicamente si el RUT del cobrador del documento coincide con el perfil tras normalizar ambos valores.

### CA-11 — PDF con texto extraíble

PdfPig extrae los datos de la plantilla de Sanatorio Alemán y el parser reconoce las filas y totales, incluso cuando la tabla continúa en otra página. Si el documento no tiene texto extraíble o no coincide con el formato, se informa el error y no se confirma una importación parcial; no se ejecuta OCR.

### CA-12 — Persistencia del PDF

El PDF original puede recuperarse después de recrear o actualizar el contenedor del backend, porque reside en un volumen persistente y no únicamente en el filesystem efímero del contenedor. La versión no contempla una copia fuera de Docker; la pérdida del host o del volumen implica la pérdida de los originales.

### CA-13 — Validar filas y totales del PDF

El parser cuenta únicamente filas con un N.º de atención válido y contrasta ese conteo con `cant. cancelaciones` cuando aparece. Suma los `Valor Pago`; el resultado debe coincidir con `TOTAL LIQUIDACIÓN` y, cuando hay subtotales de servicio, su suma también debe coincidir con ese total. `TOTAL LIQUIDACIÓN` es el bruto oficial. Si el conteo o los importes no coinciden, no se confirma la carga y no se permite editar manualmente los valores extraídos.

### CA-14 — Reconocer institución y cobrador

Los RUT pagadores `76389986-1` y `88611600-4` se clasifican bajo Sanatorio Alemán. Un RUT pagador desconocido rechaza la importación y no crea una institución ni una carga pendiente. Después de agregar el pagador mediante migración/seed, el profesional puede volver a cargar el PDF. El RUT del cobrador debe coincidir con el del profesional autenticado.

### CA-15 — Bloquear duplicados y corregir una carga

El sistema bloquea el mismo hash y también una coincidencia de profesional, RUT pagador, número de liquidación y año/mes/quincena del período de servicio impreso en el PDF. Los filtros de liquidaciones consultan el mes contable. Para corregir un PDF ya confirmado, el profesional propietario lo elimina y luego importa la versión correcta.

### CA-16 — Eliminar liquidación

Cuando el profesional propietario elimina una liquidación, se borran el PDF original y los datos asociados de la base de datos. Los agregados privados y el total combinado mensual se actualizan en la misma operación. Un administrador puede consultar/descargar, pero no eliminar la liquidación.

### CA-17 — Acceso al PDF

La descarga está autorizada únicamente para el profesional propietario y los administradores habilitados. Una URL o identificador directo no permite a otro profesional descargar el archivo.

### CA-18 — Consolidar columna institucional privada

Las liquidaciones de ambos RUT pagadores de Sanatorio Alemán se suman en una sola columna mensual de líquido. El detalle permite distinguirlas por PDF y entidad pagadora.

### CA-19 — Validar minutos por atención

El sistema acepta únicamente minutos enteros mayores que cero y rechaza cero, valores negativos y fracciones. El valor se aplica a todas las atenciones del PDF. El dashboard muestra atenciones y minutos totales, no duración promedio.

### CA-20 — Limitar tamaño del PDF

El sistema acepta archivos de hasta 1 MB (1.048.576 bytes) y rechaza archivos que superen ese tamaño antes de ejecutar PdfPig.

### CA-21 — Tratar uniformemente los servicios de la plantilla

Todas las filas de atención válidas de los servicios incluidos en la plantilla se cuentan y se consideran en la suma de `Valor Pago`. El cálculo de retención y líquido se aplica al total de la liquidación sin reglas adicionales por tipo de servicio.

### CA-22 — Mostrar columnas institucionales dinámicas

Para el intervalo seleccionado, el dashboard incluye todas las instituciones públicas o privadas con líquido mayor que cero en al menos un mes. Omite instituciones con cero durante todo el intervalo. No presenta grupos de columnas de ingresos públicos o privados ni métricas de horas, bruto, retención, atenciones o minutos.

### CA-23 — Agrupar alias públicos

Los nombres normalizados `SAPU Lorenzo Arenas` y `Lorenzo Arenas` aparecen agrupados bajo una columna `SAPU Lorenzo Arenas`. `SAR Tucapel` y `Tucapel` aparecen agrupados bajo `SAR TUCAPEL`. El líquido de cada columna canónica suma las relaciones que correspondan.

### CA-24 — Mantener el total completo al filtrar

El `Líquido combinado` mensual suma los líquidos de todas las instituciones del mes. Al filtrar columnas institucionales, el total combinado no cambia; el filtro solo afecta las columnas visibles y las series del gráfico.

### CA-25 — Importar desde un modal

El botón **Nueva liquidación** abre un modal que contiene la selección del PDF, los minutos por atención y la previsualización. Mientras se analiza o importa, se muestra el estado de carga y no se puede cerrar el modal. Cancelar limpia el borrador; un error conserva el modal abierto y muestra su detalle; una importación correcta cierra el modal y actualiza el período.

### CA-26 — Separar período de servicio y período contable

Para una liquidación cuyo período de servicio sea septiembre de 2026 y cuya fecha de liquidación sea 06-10-2026, la previsualización muestra **Período del PDF: septiembre de 2026** y **Mes contable: octubre de 2026**. Al confirmar, el bruto, la retención, el líquido y las métricas de atenciones se agregan a octubre en el workspace mensual y el dashboard. El período de servicio y la quincena originales permanecen disponibles en el detalle.

Las liquidaciones que ya estaban importadas antes de este cambio conservan su mes contable actual. La migración no las reubica; la regla por fecha se aplica a importaciones nuevas.

### CA-27 — Usar el año de la fecha de liquidación

Si el período de servicio pertenece a 2026 y la fecha de liquidación es de enero de 2027, el sistema contabiliza la liquidación en enero de 2027 y aplica la tasa anual de 2027, conservando el snapshot calculado por PDF.

## 14. Reglas de negocio

- **RN-01:** El cálculo público se mantiene basado en horas enteras y tarifas horarias por institución/año.
- **RN-02:** Las instituciones privadas se administran en un catálogo distinto del público.
- **RN-03:** Una institución privada puede tener varias entidades pagadoras con RUT distintos.
- **RN-04:** Cada PDF importado constituye una liquidación independiente.
- **RN-05:** Una liquidación privada pertenece a un profesional y a un período mensual.
- **RN-06:** El `Total Liquidación` del PDF es el bruto del profesional.
- **RN-07:** El `Factor Pago` ya está reflejado en `Valor Pago`; no se vuelve a aplicar al total del PDF.
- **RN-08:** Para Sanatorio Alemán, la retención privada se calcula por documento usando la tasa global anual del año de la fecha de liquidación.
- **RN-09:** La retención privada se redondea por PDF con `AwayFromZero`; el líquido es bruto menos retención.
- **RN-10:** La cantidad de atenciones corresponde al conteo de filas del documento, no al número de personas únicas.
- **RN-11:** Los minutos por atención se ingresan por PDF y se aplican a todas sus atenciones.
- **RN-12:** La suma mensual de líquidos privados es la suma de los líquidos calculados individualmente por PDF.
- **RN-13:** Los importes y totales se almacenan como CLP enteros exactos.
- **RN-14:** Los campos públicos del período mensual no se mezclan con los totales privados.
- **RN-15:** El líquido combinado mensual es la suma del líquido público estimado y el líquido privado calculado.
- **RN-16:** El RUT del profesional se normaliza y valida; el RUT del cobrador del PDF debe coincidir con el del propietario autenticado.
- **RN-17:** Las liquidaciones privadas se procesan inicialmente con PdfPig. Un PDF sin capa de texto no se procesa mediante OCR en esta versión.
- **RN-18:** El PDF original debe persistir fuera de la capa efímera del contenedor Docker, mediante almacenamiento persistente montado en el backend.
- **RN-19:** El conteo de atenciones se obtiene de filas con N.º de atención válido; no representa pacientes únicos.
- **RN-20:** `Total Liquidación` es el bruto autoritativo. La suma de `Valor Pago` y los subtotales disponibles se usan para validar, no para sustituir silenciosamente el total del PDF.
- **RN-21:** Si la validación del conteo, importes, RUT o formato falla, la importación no se confirma y sus campos extraídos no pueden corregirse manualmente.
- **RN-22:** Los RUT pagadores reconocidos se asocian a Sanatorio Alemán. Un RUT desconocido se rechaza; el RUT se agrega mediante migración/seed antes de volver a cargar el PDF.
- **RN-23:** Una liquidación se considera duplicada por hash o por coincidencia de profesional, RUT pagador, número de liquidación y año/mes/quincena del período de servicio impreso en el PDF. Los filtros mensuales y la consolidación usan el período contable.
- **RN-24:** Solo el profesional propietario puede eliminar una liquidación. La eliminación borra el PDF y sus datos, y actualiza los agregados del mes.
- **RN-25:** El profesional propietario y los administradores autorizados pueden descargar el PDF mediante una ruta protegida en backend.
- **RN-26:** Atenciones y duración son métricas privadas; no se combinan ni se equiparan con las horas registradas para instituciones públicas.
- **RN-27:** El dashboard muestra columnas dinámicas por institución con líquido positivo y una sola columna Sanatorio Alemán que suma sus entidades pagadoras.
- **RN-28:** Los minutos ingresados por atención deben ser enteros mayores que cero y se aplican por igual a todas las atenciones del PDF.
- **RN-29:** La primera regla de Sanatorio Alemán se aplica uniformemente a todos los servicios de su plantilla.
- **RN-30:** Los PDF cargados no pueden superar 1 MB (1.048.576 bytes).
- **RN-31:** El dashboard no calcula ni presenta el promedio mensual de duración de atención.
- **RN-32:** El dashboard omite instituciones con líquido cero en todos los meses del intervalo solicitado.
- **RN-33:** Los alias públicos acordados se agrupan y se presentan con sus etiquetas canónicas.
- **RN-34:** Los filtros de instituciones no alteran el líquido combinado mensual, que siempre suma todas las instituciones.
- **RN-35:** Para las liquidaciones privadas de Sanatorio Alemán, `Fecha Liquidación` determina el período contable y su año; el período de servicio y quincena del PDF se conservan como datos de origen. Las reglas contables de otras instituciones privadas se definirán por separado.

## 15. Decisiones pendientes

Las reglas de otras instituciones privadas y el soporte OCR para documentos escaneados son futuras ampliaciones, fuera de esta versión.
