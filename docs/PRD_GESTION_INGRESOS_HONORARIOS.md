# PRD — Gestión de ingresos por honorarios para profesionales de la salud

**Versión:** 0.10
**Alcance:** Sistema público
**Estado:** Definición inicial de producto

Este documento define el contexto funcional del producto para el equipo y para agentes de IA. La arquitectura tecnológica se describe por separado en [el estándar frontend](FRONTEND_ARCHITECTURE_STANDARD.md) y [el estándar backend](BACKEND_ARCHITECTURE_STANDARD.md). Los requisitos explícitos de este PRD definen el comportamiento del producto; las decisiones marcadas **Por definir** no deben inferirse.

## 1. Resumen del producto

La aplicación permitirá a profesionales de la salud independientes que trabajan para instituciones del sistema público llevar un control mensual de sus horas trabajadas e ingresos por boletas de honorarios.

Un profesional puede prestar servicios simultáneamente a una o varias instituciones. Cada relación profesional-institución posee un valor bruto por hora asociado a un año. Las horas trabajadas son variables y dependen del profesional.

El registro es libre dentro de un mes. No exige días, turnos, semanas, jornadas ni motivos. Por ejemplo, para septiembre:

```text
Tucapel:        1 h × 20 registros = 20 h
Lorenzo Arenas: 10 h × 2 registros = 20 h
```

Ambas formas son válidas. El sistema utiliza los registros para calcular automáticamente horas totales, ingresos brutos, retención e ingresos líquidos estimados. El objetivo no es implementar un CRUD de horas, sino ofrecer una experiencia mensual de edición rápida, flexible y sencilla.

## 2. Problema

Los profesionales pueden trabajar el mismo mes para varias instituciones públicas y actualmente controlar sus horas mediante planillas. Esto genera dificultades porque:

- Las horas cambian continuamente durante el mes.
- Los ingresos deben recalcularse manualmente.
- La tasa de retención puede cambiar por año.
- Puede haber varias instituciones activas simultáneamente.
- Las planillas no facilitan el análisis histórico ni la consolidación de ingresos.
- Un CRUD tradicional hace lento el registro y la corrección de horas.

## 3. Objetivo del producto

Permitir que un profesional responda rápidamente:

> ¿Cuánto he trabajado este mes y cuánto dinero he generado?

Flujo principal:

```text
Abrir mes → ingresar/modificar horas → ver resultados inmediatamente
```

No debe requerirse ejecutar una operación manual de cálculo.

## 4. Usuarios y autorización

### Usuario objetivo

Profesional de la salud que:

- Trabaja de manera independiente.
- Presta servicios mediante boleta de honorarios.
- Trabaja para una o varias instituciones públicas.
- Recibe un valor bruto por hora.
- Necesita controlar horas e ingresos brutos y líquidos estimados.

### Roles conocidos

El sistema contempla los roles base `ADMINISTRADOR` y `PROFESIONAL`. Una cuenta puede tener varios roles, de acuerdo con el estándar de arquitectura.

El `ADMINISTRADOR` tiene acceso de lectura a los datos de todos los profesionales y acceso total al resto del sistema. El `PROFESIONAL` solo puede consultar y modificar su propia información: perfil profesional, relaciones con instituciones, tarifas horarias propias, períodos y registros de horas. La autorización se aplica en backend, incluso si se solicita un recurso directamente por su identificador. Solo el profesional propietario puede crear nuevas versiones de su tarifa horaria; el Administrador no puede hacerlo por él.

Las instituciones públicas y la tasa anual de retención son parámetros compartidos: `PROFESIONAL` puede consultarlos en modo lectura. El catálogo de instituciones lo administra `ADMINISTRADOR`; la tabla legal de retención se carga mediante configuración controlada (seed/migración), no se edita desde la aplicación. Las tarifas horarias son personales por relación profesional-institución y no son visibles entre profesionales.

| Rol               | Alcance                                                                                                                                                                                                      |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `PROFESIONAL`   | Gestiona su perfil, relaciones y tarifas propias, períodos y horas propias. Puede leer el catálogo público y la tasa anual común. No consulta datos personales ni financieros de otros profesionales.    |
| `ADMINISTRADOR` | Puede consultar los datos de todos los profesionales y administrar las demás funciones y configuraciones del sistema. No puede crear ni modificar tarifas horarias personales; sus consultas y demás modificaciones de información personal/financiera quedan auditadas. La tasa legal global se actualiza mediante configuración controlada. |

## 5. Alcance inicial

La primera versión contempla exclusivamente prestaciones realizadas para **instituciones del sistema público**.

El sistema privado queda explícitamente fuera del MVP. Sus reglas pueden variar por institución, por ejemplo: valor por prestación, bonificaciones, porcentajes por atención, comisiones, turnos, metas o prestaciones específicas. Estas reglas deben modelarse posteriormente como otro dominio funcional, sin contaminar el cálculo público inicial.

## 6. Conceptos del dominio

### 6.1 Profesional

Persona autenticada que registra sus horas y consulta sus ingresos. Un profesional puede estar relacionado con varias instituciones y solo debe consultar/modificar información para la que tenga autorización.

### 6.2 Institución pública

Organización para la cual el profesional presta servicios; ejemplos: Tucapel, Lorenzo Arenas, Hospital Regional o CESFAM X.

Información conceptual mínima:

- Nombre.
- Estado.

El catálogo público es compartido y de lectura para profesionales; el `ADMINISTRADOR` puede administrarlo.

### 6.3 Relación profesional-institución

La configuración de trabajo asocia un profesional con una institución. El valor hora anual pertenece a esta relación, no a la institución global, porque dos profesionales pueden tener condiciones distintas en la misma institución.

### 6.4 Valor hora anual

Cada relación profesional-institución puede tener un valor hora por año. Por ejemplo:

| Institución   | Año | Valor hora bruto de ejemplo |
| -------------- | ---: | --------------------------: |
| Tucapel        | 2026 |                     $31.488 |
| Lorenzo Arenas | 2026 |                     $28.757 |

El valor no se duplica en cada registro de horas. Es un dato privado de la relación profesional-institución: un profesional no puede consultar la tarifa de otro. Solo el profesional propietario puede crear una tarifa o una nueva versión; el Administrador tiene lectura global, pero no puede crearla ni modificarla. Los valores de años/versiones anteriores se conservan para consultar períodos históricos.

### 6.5 Período mensual

Es el espacio de trabajo principal y se identifica por profesional, año y mes. Por ejemplo: septiembre de 2026. Un período contiene las instituciones en las que el profesional trabajó ese mes.

### 6.6 Instituciones del período

Cada período permite seleccionar las instituciones participantes. La selección mensual no modifica la relación general del profesional con la institución.

### 6.7 Registro libre de horas

Un registro representa únicamente:

- La institución incluida en el período.
- La cantidad de horas.
- El orden visual dentro del grupo, si la UI lo necesita.

No es obligatorio registrar día, fecha, semana, turno, jornada ni motivo. Cada registro debe contener una cantidad entera de horas de **al menos 1**. No se admiten fracciones ni cero. Registros repetidos con el mismo valor son válidos y no se deduplican.

## 7. Reglas de cálculo

Para cada institución dentro de un período:

```text
Horas totales = SUM(registros enteros de horas)
Ingreso bruto CLP = Horas totales × Valor hora anual CLP
Retención sin redondear = Ingreso bruto CLP × PERIODO_MENSUAL.retencion_porcentaje_aplicado / 100
Retención institucional CLP = redondear Retención sin redondear al peso más cercano
Líquido institucional CLP = Ingreso bruto CLP − Retención institucional CLP
```

Los resultados por institución se guardan como resumen calculado en `PERIODO_INSTITUCION`. El consolidado mensual se guarda en `PERIODO_MENSUAL` y suma los resúmenes institucionales ya redondeados.

La retención se redondea **por institución** al peso más cercano, con regla *midpoint away from zero* (mitades alejándose de cero; `MidpointRounding.AwayFromZero` en .NET). El líquido institucional se calcula restando esa retención redondeada al bruto.

Todos los montos se expresan en pesos chilenos (**CLP**, sin decimales) y se almacenan como valores enteros exactos; no usar punto flotante. La tasa porcentual conserva precisión decimal (por ejemplo, 15,25 %). Los valores líquidos siguen siendo estimados.

Los registros de horas son el detalle de origen; los campos totalizadores son valores derivados que mantiene el backend. Al agregar, editar o eliminar horas, el backend actualiza el registro, el resumen institucional y el consolidado mensual en una sola transacción. Si falla algún paso, se revierte toda la operación. La API devuelve los totales confirmados después de guardar.

## 8. Configuración anual e historia

### 8.1 Valor hora

El valor hora se consulta por relación profesional-institución y año/version. Solo el profesional propietario puede crear una tarifa o una nueva versión; el `ADMINISTRADOR` puede consultarla, pero no crearla ni modificarla. Así, septiembre de 2026 continúa usando la tarifa que tenía aplicada aunque el profesional publique otra versión para períodos posteriores.

### 8.2 Retención de honorarios

La tasa de retención de boletas de honorarios es **global y común a todos los profesionales**, con un parámetro por año en `RETENCION_BOLETA_HONORARIOS`. La tabla no pertenece a un usuario y no usa versiones de fila. Sus valores se cargan o corrigen mediante seeds/migraciones controladas, no desde la UI. Ambos roles pueden consultarla; ningún usuario la modifica desde la aplicación. El PRD incluye como ejemplo 15,25 % para 2026 y 16,00 % para 2027; esos valores deben validarse antes de cargarlos como oficiales.

### 8.3 Conservación histórica

Las consultas históricas conservan las reglas aplicadas a su período. Cada `PERIODO_INSTITUCION` referencia la versión exacta del valor hora utilizada y cada `PERIODO_MENSUAL` guarda un snapshot de `retencion_porcentaje_aplicado`. Una corrección de tarifa crea una versión nueva; una corrección legal de la tasa global se carga mediante configuración controlada. En ambos casos, los períodos existentes conservan los valores aplicados y los períodos nuevos usan la configuración vigente. No se recalculan períodos históricos silenciosamente; cualquier recálculo requiere una operación explícita y auditada.

## 9. Workspace mensual

La pantalla principal se comporta como un espacio de trabajo, no como una lista de registros con navegación CRUD. Agrupa las instituciones del mes y muestra registros y resultados en el mismo contexto.

Ejemplo de estructura:

```text
                         SEPTIEMBRE 2026

       TUCAPEL                              LORENZO ARENAS
       $31.488/h                            $28.757/h

       [ 6 ]                                [ 9 ]
       [ 6 ]                                [ 9 ]
       [ 6 ]                                [ 9 ]
       + agregar horas                      + agregar horas

       42 horas                             27 horas
       $1.322.496 bruto                     $776.439 bruto
       $1.120.815 líquido est.              $658.032 líquido est.

                         RESUMEN DEL MES
       Horas: 69 · Bruto: $2.098.935 · Retención: $320.088 ·
       Líquido estimado: $1.778.847
```

Cada institución mostrará como mínimo nombre, valor hora, cantidad de registros, horas totales, bruto, retención estimada y líquido estimado. El total consolidado del mes permanece visible.

## 10. Interacción y UX

### Agregar, editar y eliminar

- Agregar un registro desde el final del grupo con acción `+ agregar horas`.
- Dejar el nuevo valor disponible inmediatamente para escribir.
- Editar el valor inline sin abrir una pantalla o modal.
- Eliminar cada registro rápidamente, sin navegación a otra pantalla.
- Permitir cualquier cantidad de registros desde la regla de negocio; límites técnicos de tamaño/consumo no deben convertirse en un límite funcional arbitrario.

### Teclado

El editor debe permitir una experiencia similar a una planilla. Como mínimo, considerar `Enter`, `Tab`, flechas y `Backspace/Delete`. Ejemplo: escribir `6`, presionar `Enter` y recibir inmediatamente una fila nueva para continuar.

### Autoguardado

No depender de un botón Guardar para cada cambio. Mostrar estados claros:

```text
Guardando… → Guardado
             Error al guardar
```

La estrategia para cambios simultáneos, reintentos, desconexión y conflictos entre pestañas/dispositivos está **Por definir**. El producto debe evitar que un error de red dé la impresión de que un cambio no guardado sí quedó persistido.

El cambio de la fila de horas, los totales de su institución y el consolidado mensual se confirman juntos. La interfaz muestra `Guardado` solo después de que el backend confirme la transacción y devuelva los nuevos totales.

### Recálculo

Agregar, editar o eliminar horas actualiza inmediatamente las horas e importes visibles. La UI puede mostrar un cálculo optimista mientras guarda; el resultado confirmado por backend es autoritativo.

## 11. Navegación y selección de instituciones

- Navegación entre meses anterior y siguiente.
- Selector por mes y año.
- Cada mes permite seleccionar las instituciones en que se trabajó.
- Agregar una institución a un mes no cambia la configuración general del profesional.
- Una institución solo puede retirarse del período si no tiene registros de horas; no se permite eliminarla en cascada junto con esos registros.
- No se presupone que todas las instituciones estén activas en todos los períodos.

## 12. Historias de usuario

| ID    | Historia                                                                                                                             |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------ |
| HU-01 | Como profesional, quiero registrar una institución pública para asociarle posteriormente mis horas.                                |
| HU-02 | Como profesional, quiero definir el valor hora de una institución para un año determinado para calcular ingresos automáticamente. |
| HU-03 | Como profesional, quiero acceder a un mes específico para revisar horas e ingresos.                                                 |
| HU-04 | Como profesional, quiero indicar en qué instituciones trabajé durante el mes.                                                      |
| HU-05 | Como profesional, quiero agregar libremente cantidades de horas sin asociarlas obligatoriamente a fecha o jornada.                   |
| HU-06 | Como profesional, quiero editar inline cualquier cantidad ingresada.                                                                 |
| HU-07 | Como profesional, quiero eliminar un registro ingresado incorrectamente.                                                             |
| HU-08 | Como profesional, quiero ver automáticamente la suma de horas por institución.                                                     |
| HU-09 | Como profesional, quiero ver automáticamente el ingreso bruto por institución.                                                     |
| HU-10 | Como profesional, quiero ver un líquido estimado con la retención correspondiente al año.                                         |
| HU-11 | Como profesional, quiero ver el resumen consolidado del mes.                                                                         |
| HU-12 | Como profesional, quiero consultar períodos anteriores sin alterar sus cálculos históricos.                                       |
| HU-13 | Como profesional, quiero que mis períodos, tarifas y horas sean visibles solo para mí y para administradores autorizados.          |
| HU-14 | Como administrador, quiero consultar los datos de todos los profesionales y administrar el sistema, sin crear tarifas horarias en su nombre. |
| HU-15 | Como profesional, quiero consultar la tasa de retención anual común sin poder modificarla.                                         |
| HU-16 | Como profesional, quiero crear o corregir únicamente las tarifas de mis propias relaciones con instituciones.                         |
| HU-17 | Como profesional, quiero comparar en una tabla el líquido y su participación mensual por institución durante un intervalo de años.       |
| HU-18 | Como profesional, quiero ver la evolución mensual del líquido por institución y el total en un gráfico lineal.                          |
| HU-19 | Como administrador, quiero seleccionar un profesional y consultar su tabla y evolución mensual de líquido por institución.            |

## 13. Requisitos funcionales

| ID    | Requisito                                                                                                              |
| ----- | ---------------------------------------------------------------------------------------------------------------------- |
| RF-01 | Crear y administrar instituciones públicas.                                                                           |
| RF-02 | Asociar instituciones al profesional.                                                                                  |
| RF-03 | Permitir que cada profesional defina/versione el valor hora de sus relaciones por año; Administrador tiene lectura, no escritura. |
| RF-04 | Aplicar la tasa legal común del año del período desde configuración controlada.                                       |
| RF-05 | Acceder a períodos mensuales.                                                                                         |
| RF-06 | Asociar instituciones participantes a un período.                                                                     |
| RF-07 | Registrar libremente horas sin requerir fecha.                                                                         |
| RF-08 | Editar horas inline.                                                                                                   |
| RF-09 | Eliminar registros de horas.                                                                                           |
| RF-10 | Calcular horas totales automáticamente.                                                                               |
| RF-11 | Calcular ingreso bruto automáticamente.                                                                               |
| RF-12 | Calcular retención según el año automáticamente.                                                                   |
| RF-13 | Calcular líquido estimado.                                                                                            |
| RF-14 | Calcular el consolidado mensual.                                                                                       |
| RF-15 | Consultar períodos históricos.                                                                                       |
| RF-16 | Navegar rápidamente entre meses.                                                                                      |
| RF-17 | Aislar en backend los datos por profesional; Administrador consulta todos, con escritura de tarifas reservada al propietario. |
| RF-18 | Exponer la tasa anual común en modo lectura; no permitir editarla desde la aplicación.                                  |
| RF-19 | Conservar la versión de tarifa y el snapshot de la tasa aplicada a cada período.                                       |
| RF-20 | Persistir y actualizar transaccionalmente los totales por institución y del período al cambiar horas.                    |
| RF-21 | Rechazar el retiro de una institución del período si contiene registros de horas.                                         |
| RF-22 | Mostrar en el Dashboard una tabla mensual por institución, con líquido, participación porcentual del total del mes y total mensual. |
| RF-23 | Mostrar un gráfico lineal de líquido mensual por institución y una serie con el total para el intervalo de años seleccionado. |

## 14. Criterios de aceptación

### CA-01 — Registro libre

Dado un período con Tucapel, al registrar cuatro filas de una hora, el sistema muestra cuatro registros y cuatro horas totales.

### CA-02 — Distribución arbitraria

Si dos profesionales registran 20 horas para la misma relación tarifaria, uno como veinte filas de una hora y otro como dos filas de diez horas, ambos obtienen 20 horas y el mismo bruto.

### CA-03 — Recálculo al editar

Si existen 20 horas y una fila de una hora se cambia a dos, el total pasa inmediatamente a 21 y los importes se actualizan.

### CA-04 — Recálculo al eliminar

Si existen dos filas de diez horas y se elimina una, el total pasa a diez y bruto, retención y líquido se actualizan.

### CA-05 — Cambio de año

Un período de diciembre de 2026 utiliza la tarifa y tasa de 2026; enero de 2027 utiliza las configuraciones de 2027.

### CA-06 — Aislamiento por profesional

Dado que dos profesionales tienen períodos, tarifas y horas distintas, un usuario con rol `PROFESIONAL` solo obtiene y modifica sus propios datos. Una solicitud directa por el identificador de un recurso de otro profesional no devuelve su contenido ni permite modificarlo. Un `ADMINISTRADOR` puede consultar los datos de todos, pero no crear ni modificar tarifas horarias pertenecientes a profesionales.

### CA-07 — Horas enteras

El sistema acepta registros de horas enteras de 1 o más. Rechaza cero, valores negativos y fracciones; acepta registros repetidos con la misma cantidad.

### CA-08 — Cálculo y redondeo CLP por institución

Con tarifa de $31.488 CLP/h, 42 horas y tasa de 15,25 %, el bruto es $1.322.496, la retención se redondea desde $201.680,64 a $201.681 y el líquido estimado es $1.120.815. El consolidado mensual suma las retenciones y líquidos institucionales ya redondeados.

### CA-09 — Retención global de solo lectura

La tasa anual común puede consultarse en modo lectura. No se puede modificar desde la aplicación; se actualiza mediante configuración controlada. Cada período conserva la tasa que usó.

### CA-10 — Conservación de versiones

Si se publica una nueva versión de tarifa o se corrige la tasa global mediante configuración controlada, los períodos existentes conservan el valor aplicado en su snapshot y los períodos nuevos usan la configuración vigente. Una corrección no altera silenciosamente cálculos históricos.

### CA-11 — Propiedad de la tarifa horaria

El profesional propietario puede crear una tarifa y publicar una nueva versión solo para su relación con la institución. Otro profesional no puede consultar ni modificar esa tarifa; el `ADMINISTRADOR` puede consultarla, pero no crearla, editarla ni versionarla.

### CA-12 — No retirar instituciones con horas

Dada una institución incluida en un período con uno o más registros de horas, cuando se intenta retirarla, el backend rechaza la operación con `409 Conflict`. Los registros y totales se conservan. Si no hay registros, se permite retirarla y se actualiza el consolidado mensual en la misma transacción.

### CA-13 — Totales persistidos y atómicos

Al agregar, editar o eliminar un registro, el sistema persiste los totales actualizados de la institución y del período en una sola transacción. Si falla el guardado del registro o cualquiera de los agregados, la operación completa se revierte. Los totales guardados coinciden con la suma de los registros.

### CA-14 — Tabla mensual de líquido por institución

Para el intervalo de años seleccionado, el Dashboard muestra los meses agrupados por año. Cada institución tiene una columna con su líquido CLP y su participación porcentual del líquido total de ese mes; la última columna muestra el total mensual. El porcentaje es `líquido de la institución / líquido total mensual × 100` y no se persiste. Si el total mensual es cero, mostrar `—` como porcentaje.

### CA-15 — Gráfico lineal de evolución

El gráfico muestra una serie mensual por institución y una serie del total mensual. Usa el mismo intervalo de años y filtro de instituciones que la tabla. El total de cada punto mensual coincide con la suma de los importes institucionales de esa fila.

### CA-16 — Alcance y ausencia de datos

`PROFESIONAL` solo consulta sus propios datos. `ADMINISTRADOR` elige un profesional; no se combinan ingresos de varias personas. Si no existe `PERIODO_MENSUAL` para un mes, la tabla deja la celda sin dato y el gráfico muestra un hueco. Si el período existe pero una institución no fue vinculada, su valor es CLP $0. Si un período existente tiene total cero, la participación porcentual muestra `—`.

## 15. Pantallas del MVP

El MVP puede organizarse en cinco áreas principales:

1. Dashboard.
2. Mes de trabajo — pantalla fundamental.
3. Instituciones.
4. Tarifas/valores hora.
5. Configuración.

### Dashboard — tabla y evolución del líquido

El Dashboard permite analizar el líquido por institución a través de meses y años. La vista principal es una tabla tipo planilla; un gráfico lineal la complementa. No se utiliza gráfico circular.

#### Filtros y alcance

- Seleccionar un intervalo de años; las filas se agrupan por año y mes.
- Filtrar por todas las instituciones o por una o varias instituciones.
- `PROFESIONAL` consulta sus propios datos. `ADMINISTRADOR` selecciona un profesional; no se agregan ingresos de personas distintas.

#### Tabla mensual

- Una fila por cada mes del intervalo, ordenada cronológicamente, una columna por institución y una columna de total líquido mensual.
- Cada celda institucional muestra el importe líquido en CLP sin decimales y su participación sobre el total líquido de ese mes, por ejemplo `$1.120.815 · 63,0 %`.
- El porcentaje se calcula al presentar la tabla: `líquido institucional / líquido mensual × 100`; no se persiste. La precisión visual queda por definir, con una cifra decimal como recomendación.
- El total mensual es la suma de los líquidos institucionales y debe coincidir con `PERIODO_MENSUAL.liquido_total_clp`.
- Si no existe `PERIODO_MENSUAL`, mostrar hueco/sin dato. Si el período existe pero una institución no está vinculada, mostrar CLP $0. Si el total es cero, mostrar `—` como porcentaje.

Ejemplo para septiembre de 2026:

| Año | Mes | Tucapel — líquido / participación | Lorenzo Arenas — líquido / participación | Total líquido |
|---:|---|---:|---:|---:|
| 2026 | Septiembre | $1.120.815 · 63,0 % | $658.032 · 37,0 % | $1.778.847 |

#### Gráfico lineal

- Eje horizontal: meses consecutivos del intervalo seleccionado; eje vertical: líquido CLP.
- Una serie por institución y una serie para el total mensual.
- Usa los mismos filtros de intervalo, institución y profesional que la tabla.
- Un mes sin período aparece como hueco/sin dato; un período existente sin ingresos se representa como cero.

La tabla y el gráfico leen `PERIODO_INSTITUCION.liquido_total_clp` y `PERIODO_MENSUAL.liquido_total_clp`. Reutilizan los totales persistidos y las versiones históricas aplicadas; no recalculan períodos anteriores con tarifas o tasas actuales. No se agrega una tabla de analítica.

## 16. Fuera del alcance del MVP

- Instituciones privadas y sus reglas particulares.
- Remuneraciones dependientes y contratos laborales.
- Previsión, Fonasa/Isapre, AFP o licencias médicas.
- Emisión automática de boletas, integración con SII, conciliación bancaria, facturación o pagos efectivamente recibidos.
- Agenda clínica, pacientes, prestaciones médicas, turnos obligatorios o control de asistencia.
- Registro obligatorio por día.

## 17. Métricas futuras

El MVP incluye la tabla multi-año de líquido mensual por institución y el gráfico lineal por institución más el total. Se consideran posteriores estas analíticas adicionales:

- Comparación automática con el mes anterior o con el mismo mes de años previos.
- Participación porcentual acumulada por institución para todo un año o intervalo.
- Valor líquido promedio por hora y ranking de instituciones por ingresos.
- Proyección de ingresos y horas.
- Exportación de resultados a CSV.

Estas métricas no amplían por sí mismas el alcance del MVP.

## 18. Diagrama relacional DBML

El siguiente modelo está expresado en **DBML**, listo para pegar en [dbdiagram.io](https://dbdiagram.io/). Mantiene las relaciones de dominio y propiedad sin tablas relacionales de permisos ni de eventos de auditoría.

```dbml
Project gestion_ingresos_honorarios {
  database_type: 'PostgreSQL'
  Note: 'ADMINISTRADOR tiene lectura global; solo el PROFESIONAL propietario escribe su tarifa. PROFESIONAL accede solo a sus datos propios.'
}

Table usuarios {
  id uuid [pk]
  identity_subject varchar(255) [not null, unique]
  password_hash text [note: 'Hash administrado por ASP.NET Core Identity; nullable si la autenticación se delega a OIDC. Nunca guardar la contraseña en claro.']
  activo boolean [not null, default: true]
  created_at timestamptz [not null]
}

Table profesionales {
  id uuid [pk]
  usuario_id uuid [not null, unique]
  nombre varchar(200) [not null]
  created_at timestamptz [not null]
}

Table roles {
  id uuid [pk]
  codigo varchar(50) [not null, unique, note: 'ADMINISTRADOR o PROFESIONAL']
  nombre varchar(100) [not null]
}

Table usuarios_roles {
  usuario_id uuid [not null]
  rol_id uuid [not null]
  asignado_at timestamptz [not null]

  indexes {
    (usuario_id, rol_id) [pk]
  }
}

Table instituciones_publicas {
  id uuid [pk]
  nombre varchar(200) [not null]
  activa boolean [not null, default: true]
  created_at timestamptz [not null]
}

Table profesionales_instituciones {
  id uuid [pk]
  profesional_id uuid [not null]
  institucion_publica_id uuid [not null]
  activa boolean [not null, default: true]
  created_at timestamptz [not null]

  indexes {
    (profesional_id, institucion_publica_id) [unique]
    (id, profesional_id) [unique]
  }
}

Table tarifas_hora_anuales_versiones {
  profesional_institucion_id uuid [not null]
  anio smallint [not null]
  version integer [not null]
  valor_hora_clp bigint [not null, note: 'CLP entero, sin decimales']
  created_at timestamptz [not null]

  indexes {
    (profesional_institucion_id, anio, version) [pk]
  }

  Note: 'Tarifa privada; solo el profesional propietario crea nuevas versiones. La versión más alta es la vigente para nuevos períodos.'
}

Table retencion_boleta_honorarios {
  anio smallint [pk]
  porcentaje numeric(7,4) [not null, note: 'Parámetro legal global; por ejemplo, 15.25']

  Note: 'Una fila global por año. Se mantiene por seed/migración controlada; no pertenece a usuarios ni tiene versionado por fila.'
}

Table periodos_mensuales {
  id uuid [pk]
  profesional_id uuid [not null]
  anio smallint [not null]
  mes smallint [not null]
  retencion_porcentaje_aplicado numeric(7,4) [not null, note: 'Snapshot de la tasa global al crear el período']
  total_horas bigint [not null, default: 0]
  bruto_total_clp bigint [not null, default: 0]
  retencion_total_clp bigint [not null, default: 0]
  liquido_total_clp bigint [not null, default: 0]
  created_at timestamptz [not null]
  updated_at timestamptz [not null]

  indexes {
    (profesional_id, anio, mes) [unique]
    (id, profesional_id, anio) [unique]
  }

  checks {
    `mes >= 1 AND mes <= 12` [name: 'chk_periodos_mensuales_mes']
  }
}

Table periodos_instituciones {
  id uuid [pk]
  periodo_id uuid [not null]
  profesional_id uuid [not null, note: 'Campo de ámbito para validar propietario con FK compuesta']
  anio smallint [not null, note: 'Debe coincidir con el período y la tarifa']
  profesional_institucion_id uuid [not null]
  tarifa_hora_version integer [not null]
  total_horas bigint [not null, default: 0]
  bruto_total_clp bigint [not null, default: 0]
  retencion_total_clp bigint [not null, default: 0]
  liquido_total_clp bigint [not null, default: 0]
  orden integer [not null, default: 0]
  created_at timestamptz [not null]
  updated_at timestamptz [not null]

  indexes {
    (periodo_id, profesional_institucion_id) [unique]
  }

  Note: 'Referencia la tarifa aplicada y persiste el resumen calculado de la institución.'
}

Table registros_horas {
  id uuid [pk]
  periodo_institucion_id uuid [not null]
  horas integer [not null, note: 'Entero, sin fecha']
  orden integer [not null]
  created_at timestamptz [not null]
  updated_at timestamptz [not null]

  indexes {
    (periodo_institucion_id, orden) [unique]
  }

  checks {
    `horas >= 1` [name: 'chk_registros_horas_minimo_uno']
  }

  Note: 'No añadir unicidad por horas: cantidades repetidas son registros válidos.'
}

Table sesiones_auth {
  id uuid [pk]
  usuario_id uuid [not null]
  sid uuid [not null, unique]
  familia_refresh_id uuid [not null]
  refresh_token_hash varchar(128) [not null]
  creada_at timestamptz [not null]
  ultimo_uso_at timestamptz [not null]
  expira_inactividad_at timestamptz [not null]
  expira_absoluta_at timestamptz [not null]
  revocada_at timestamptz

  Note: 'Guardar el hash del refresh token, nunca el token en claro.'
}

Ref: profesionales.usuario_id - usuarios.id

Ref: usuarios_roles.usuario_id > usuarios.id
Ref: usuarios_roles.rol_id > roles.id

Ref: profesionales_instituciones.profesional_id > profesionales.id
Ref: profesionales_instituciones.institucion_publica_id > instituciones_publicas.id

Ref: tarifas_hora_anuales_versiones.profesional_institucion_id > profesionales_instituciones.id
Ref: periodos_mensuales.profesional_id > profesionales.id
Ref: periodos_mensuales.anio > retencion_boleta_honorarios.anio

Ref: periodos_instituciones.(periodo_id, profesional_id, anio) > periodos_mensuales.(id, profesional_id, anio)
Ref: periodos_instituciones.(profesional_institucion_id, profesional_id) > profesionales_instituciones.(id, profesional_id)
Ref: periodos_instituciones.(profesional_institucion_id, anio, tarifa_hora_version) > tarifas_hora_anuales_versiones.(profesional_institucion_id, anio, version)

Ref: registros_horas.periodo_institucion_id > periodos_instituciones.id [delete: restrict]
Ref: sesiones_auth.usuario_id > usuarios.id
```

La autorización se resuelve en el backend mediante reglas asociadas a `ADMINISTRADOR` y `PROFESIONAL`; no se almacenan catálogos de permisos por acción en tablas. Los accesos y cambios administrativos se registran en logging estructurado/centralizado, no en una tabla relacional de auditoría.

### Entidades y atributos conceptuales

| Entidad | Atributos principales | Propósito |
|---|---|---|
| `USUARIO` | `id`, `identity_subject`, `password_hash` opcional, `activo` | Identidad autenticada; puede tener varios roles. La contraseña solo se conserva como hash de ASP.NET Core Identity, nunca en texto claro. |
| `PROFESIONAL` | `id`, `usuario_id` único, datos del perfil | Perfil propio de una cuenta con rol profesional. |
| `ROL`, `USUARIO_ROL` | Código y asignaciones | Roles `ADMINISTRADOR` y `PROFESIONAL`; una cuenta puede tener ambos. |
| `INSTITUCION_PUBLICA` | `id`, nombre, estado | Catálogo global de lectura para profesionales; lo administra el Administrador. |
| `PROFESIONAL_INSTITUCION` | Profesional, institución, estado | Relación de trabajo y ámbito de propiedad de la tarifa. |
| `TARIFA_HORA_ANUAL_VERSION` | Relación profesional-institución, año, versión, valor CLP entero | Tarifa privada por profesional, institución y año; solo el propietario la crea/versiona. |
| `RETENCION_BOLETA_HONORARIOS` | Año, porcentaje decimal | Parámetro legal global, una fila por año, cargado por configuración controlada. |
| `PERIODO_MENSUAL` | Profesional, año, mes, tasa aplicada y totales consolidados | Espacio mensual, dueño de datos y resumen persistido del período. |
| `PERIODO_INSTITUCION` | Período, profesional, relación, tarifa aplicada y totales | Institución participante y resumen persistido por institución. |
| `REGISTRO_HORA` | Período-institución, horas enteras, orden, timestamps | Fila sin fecha; horas `>= 1`; permite valores repetidos. |
| `SESION_AUTH` | Usuario, `sid`, hash refresh, familia, expiraciones, revocación | Sesiones revocables; nunca almacenar el refresh token en claro. |

### Claves, restricciones y propiedad de datos

- `PROFESIONAL.usuario_id` es único: una cuenta tiene como máximo un perfil profesional.
- `PROFESIONAL_INSTITUCION` es único por `(profesional_id, institucion_id)`.
- `TARIFA_HORA_ANUAL_VERSION` se identifica por relación profesional-institución, año y versión. Solo el profesional propietario puede crear una nueva versión; Administrador puede consultar pero no escribir esas tarifas.
- `RETENCION_BOLETA_HONORARIOS` tiene una fila global por año y no contiene `profesional_id`, `version` ni usuario creador.
- `PERIODO_MENSUAL` es único por `(profesional_id, anio, mes)`, referencia el año de la tabla legal y conserva `retencion_porcentaje_aplicado` como snapshot.
- El snapshot de retención de un período no cambia si posteriormente se corrige el valor global cargado para ese año.
- `PERIODO_INSTITUCION` es único por `(periodo_id, profesional_institucion_id)` y referencia la versión exacta de tarifa usada. Su período, relación profesional-institución y tarifa deben corresponder al mismo profesional y año.
- Para reforzar la propiedad en la base, se usarán claves foráneas compuestas con `profesional_id` (y año cuando aplique) en `PERIODO_INSTITUCION`. Esa columna puede repetirse para que PostgreSQL valide que período y relación pertenecen a la misma persona.
- `REGISTRO_HORA.horas` es un entero `CHECK (horas >= 1)`. No se crea unicidad por cantidad de horas. `orden` permite mantener el orden visual.
- No se puede eliminar `PERIODO_INSTITUCION` mientras tenga registros en `REGISTRO_HORA`. La FK usa `ON DELETE RESTRICT`; el backend rechaza el intento con `409 Conflict`.
- Las versiones de tarifas horarias no se editan en sitio. Una corrección crea una nueva versión y los períodos existentes conservan la tarifa aplicada.
- La tasa legal global se administra fuera de la aplicación mediante seed/migración. `PERIODO_MENSUAL.retencion_porcentaje_aplicado` conserva el valor usado aunque el parámetro global cambie.
- Los totales son agregados derivados y persistidos en `PERIODO_INSTITUCION` y `PERIODO_MENSUAL`. No son editables desde la API ni se guardan en cada registro de hora; el detalle de `REGISTRO_HORA` es la fuente para recalcularlos.
- Cada mutación de horas actualiza el registro y ambos niveles de resumen en la misma transacción. Una falla revierte todos los cambios; una rutina de reconciliación puede reconstruir los agregados desde el detalle.
- Las tarifas y todos los importes se guardan como enteros exactos en CLP (escala 0); la tasa de retención conserva precisión decimal porque es un porcentaje, no un monto.

### Ámbito de visibilidad

| Rol               | Acceso                                                                                                                                                                         |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `PROFESIONAL`   | CRUD de sus relaciones, tarifas propias, períodos y registros; lectura del catálogo público y tasa anual común. Ningún dato personal o financiero de otros profesionales. |
| `ADMINISTRADOR` | Lectura de todos los datos y administración del sistema; no crea ni modifica tarifas horarias de profesionales. Otras consultas/cambios sensibles quedan sujetas a auditoría. |

El backend obtiene el `profesional_id` desde el sujeto autenticado. Nunca acepta el dueño como autoridad desde body, query, ruta o estado del frontend. Cada consulta y mutación valida propiedad en backend; los IDs no evitan el aislamiento.

## 19. Reglas de negocio

- **RN-01:** Un profesional puede tener varias instituciones públicas.
- **RN-02:** Una relación profesional-institución puede tener un valor hora diferente por año.
- **RN-03:** Dentro del año, se utiliza el valor hora configurado para ese año.
- **RN-04:** Los registros de horas pertenecen a un período mensual.
- **RN-05:** Cada registro pertenece a una institución participante del período.
- **RN-06:** No es obligatorio asociar una fecha a un registro.
- **RN-07:** No existe límite funcional para la cantidad de registros de horas del profesional.
- **RN-08:** Registros con la misma cantidad son válidos y no se deduplican.
- **RN-09:** El total de horas es calculado: suma de los registros.
- **RN-10:** El bruto es calculado: horas totales × valor hora aplicable.
- **RN-11:** La retención depende del año del período.
- **RN-12:** El líquido se presenta como estimado.
- **RN-13:** Modificar un registro recalcula los resultados.
- **RN-14:** Eliminar un registro recalcula los resultados.
- **RN-15:** Los períodos históricos conservan las reglas que les correspondían; las correcciones no son silenciosas.
- **RN-16:** `PROFESIONAL` solo puede consultar/modificar información propia. `ADMINISTRADOR` puede consultar datos de todos y administrar el sistema, con excepción de la escritura de tarifas horarias personales; el backend valida la propiedad en toda operación.
- **RN-17:** El valor hora es privado por relación profesional-institución y año/version. Solo el profesional propietario crea/versiona la tarifa; Administrador tiene lectura, no escritura.
- **RN-18:** La tasa de retención es un parámetro legal común, con una fila global fija por año. Se lee desde la aplicación y se actualiza mediante configuración controlada, no desde la UI.
- **RN-19:** Cada registro de horas es un entero mayor o igual a 1. No se permiten fracciones ni cero.
- **RN-20:** La moneda es CLP sin decimales. Los montos se almacenan como valores enteros exactos.
- **RN-21:** Redondear la retención al peso más cercano por institución con `MidpointRounding.AwayFromZero`; líquido institucional es bruto menos retención redondeada y el resumen mensual suma resultados institucionales.
- **RN-22:** Cada período conserva la versión exacta de la tarifa horaria y un snapshot del porcentaje de retención aplicado.
- **RN-23:** Cada alta, edición o eliminación de horas actualiza en una transacción el detalle, el resumen de la institución y el consolidado del período.
- **RN-24:** No se puede retirar una institución de un período si tiene registros de horas; la eliminación en cascada está prohibida.

## 20. Principio de diseño del producto

> El sistema no decide cómo el profesional debe descomponer sus horas. El profesional elige la forma de registrarlas; el sistema las suma, calcula y presenta.

La experiencia buscada es abrir el mes, escribir valores inline, usar `Enter` para continuar y ver los resultados actualizados, no completar un formulario y regresar a una lista después de cada registro.

## 21. Definición del MVP

El MVP 1.0 se considera funcional cuando un profesional autorizado puede:

1. Crear/seleccionar sus instituciones públicas.
2. Definir su valor hora anual por institución.
3. Abrir un período mensual y agregar sus instituciones participantes.
4. Crear libremente cualquier cantidad de registros de horas y editarlos/eliminarlos rápidamente.
5. Ver horas acumuladas, bruto, retención del año y líquido estimado por institución y como consolidado.
6. Consultar períodos históricos con las reglas correspondientes al período.
7. Guardar los totales calculados por institución y período, actualizados junto con cada mutación de horas.
8. Impedir el retiro de una institución del mes mientras tenga registros de horas.

El núcleo del producto es:

```text
Institución + valor hora anual + período mensual + registros libres de horas
                                ↓
                  cálculos y consolidado mensual
```

## 22. Decisiones pendientes

Estas decisiones requieren definición antes de implementar las reglas correspondientes:

1. **Autoguardado:** debounce, feedback, reintentos, edición concurrente y resolución de conflictos entre pestañas/dispositivos.
2. **Orden y reversión:** si se permite reordenar manualmente las filas y si se requiere deshacer/restaurar eliminaciones.
3. **Límites técnicos:** elegir tipos de almacenamiento y límites de entrada suficientemente amplios sin imponer un límite funcional de registros.
4. **Porcentaje en el Dashboard:** definir la precisión visual (se recomienda una cifra decimal).
5. **Datos de prueba tributarios:** validar tasas anuales antes de considerarlas oficiales.

Hasta resolverlas, la IA debe conservarlas como decisiones abiertas y no inventar comportamientos.

## 23. Directrices para agentes de IA

- Tratar este PRD como fuente funcional de verdad para el módulo público.
- No convertir el workspace mensual en un flujo CRUD de formularios/modal por registro.
- No exigir día, turno, semana, jornada ni motivo a los registros de horas.
- No deduplicar filas repetidas ni imponer un límite funcional de cantidad de registros.
- No hardcodear tasas tributarias o valores hora; conservar su año y su contexto profesional-institución.
- No añadir reglas del sector privado ni emitir boletas o pagos reales en el MVP.
- Respetar el acceso global de lectura de `ADMINISTRADOR` y el aislamiento propio de `PROFESIONAL`; solo el profesional propietario puede escribir/versionar su tarifa.
- Respetar horas enteras `>= 1`, CLP sin decimales, redondeo por institución y versiones históricas aplicadas.
- El Dashboard usa una tabla mensual multi-año con líquido/participación por institución y una línea de líquido por institución más el total.
- Aplicar filtros de instituciones y profesional respetando el alcance del rol; no consolidar ingresos de profesionales distintos.
- Si no existe período mensual, mostrar hueco/sin datos; si el período existe y una institución no fue asociada, mostrar cero.
- Actualizar transaccionalmente los registros y los dos niveles de totales; nunca aceptar totales editables desde el frontend.
- No permitir retirar una institución de un período si contiene registros; no borrar detalles mediante cascada.
- No inventar comportamientos para decisiones que siguen pendientes: conflicto/autoguardado, reversión/reorden y límites técnicos de almacenamiento.
- Aplicar la autorización del backend definida en los estándares; guards y componentes React solo controlan la experiencia visual.
- Mantener los cálculos mostrados actualizados; el backend es la autoridad del resultado persistido.
- Si un requisito técnico parece contradecir una regla de producto, describir el conflicto y pedir decisión antes de cambiar el comportamiento.
