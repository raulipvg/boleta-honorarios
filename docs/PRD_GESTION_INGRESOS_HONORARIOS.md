# PRD — Gestión de ingresos por honorarios para profesionales de la salud

**Versión:** 0.1
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

La matriz de permisos para pantallas, acciones y administración de datos está **Por definir**. No asumir que un profesional puede ver datos de otro, ni que el administrador omite controles de autorización por recurso.

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

El catálogo y quién puede crear/editar instituciones están **Por definir**.

### 6.3 Relación profesional-institución

La configuración de trabajo asocia un profesional con una institución. El valor hora anual pertenece a esta relación, no a la institución global, porque dos profesionales pueden tener condiciones distintas en la misma institución.

### 6.4 Valor hora anual

Cada relación profesional-institución puede tener un valor hora por año. Por ejemplo:

| Institución   | Año | Valor hora bruto de ejemplo |
| -------------- | ---: | --------------------------: |
| Tucapel        | 2026 |                     $31.488 |
| Lorenzo Arenas | 2026 |                     $28.757 |

El valor no se duplica en cada registro de horas. Los valores de años anteriores deben conservarse para consultar períodos históricos.

### 6.5 Período mensual

Es el espacio de trabajo principal y se identifica por profesional, año y mes. Por ejemplo: septiembre de 2026. Un período contiene las instituciones en las que el profesional trabajó ese mes.

### 6.6 Instituciones del período

Cada período permite seleccionar las instituciones participantes. La selección mensual no modifica la relación general del profesional con la institución.

### 6.7 Registro libre de horas

Un registro representa únicamente:

- La institución incluida en el período.
- La cantidad de horas.
- El orden visual dentro del grupo, si la UI lo necesita.

No es obligatorio registrar día, fecha, semana, turno, jornada ni motivo. Registros repetidos con el mismo valor son válidos y no se deduplican. El PRD no fija aún si se permiten fracciones de hora ni su precisión.

## 7. Reglas de cálculo

Para cada institución dentro de un período:

```text
Horas totales = SUM(registros de horas)
Ingreso bruto = Horas totales × Valor hora aplicable al año
Retención estimada = Ingreso bruto × Tasa de retención del año
Líquido estimado = Ingreso bruto − Retención estimada
```

El resumen mensual consolida las horas, el bruto, la retención y el líquido estimado de todas las instituciones del período.

Las reglas de redondeo monetario y precisión interna de horas están **Por definir**. Los cálculos deben evitar errores de punto flotante y los ejemplos no deben usarse para inferir silenciosamente una regla de redondeo.

## 8. Configuración anual e historia

### 8.1 Valor hora

El valor hora se consulta por relación profesional-institución y año. Así, septiembre de 2026 continúa usando la configuración 2026 aunque exista otra para 2027.

### 8.2 Retención de honorarios

La tasa de retención se administra por año y se selecciona a partir del año del período. No se hardcodea en frontend ni en lógica de negocio. El PRD incluye como ejemplo 15,25 % para 2026 y 16,00 % para 2027.

### 8.3 Conservación histórica

Las consultas históricas deben conservar las reglas aplicables a su período. Queda **Por definir** cómo tratar cambios retroactivos o correcciones de una tarifa/tasa ya utilizada: conservar una versión de configuración, congelar un snapshot aplicado al período o recalcular con auditoría explícita. Ninguna modificación debe cambiar resultados históricos silenciosamente.

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

### Recálculo

Agregar, editar o eliminar horas actualiza inmediatamente las horas e importes visibles. La UI puede mostrar un cálculo optimista mientras guarda; el resultado confirmado por backend es autoritativo.

## 11. Navegación y selección de instituciones

- Navegación entre meses anterior y siguiente.
- Selector por mes y año.
- Cada mes permite seleccionar las instituciones en que se trabajó.
- Agregar una institución a un mes no cambia la configuración general del profesional.
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

## 13. Requisitos funcionales

| ID    | Requisito                                                            |
| ----- | -------------------------------------------------------------------- |
| RF-01 | Crear y administrar instituciones públicas.                         |
| RF-02 | Asociar instituciones al profesional.                                |
| RF-03 | Definir el valor hora por relación profesional-institución y año. |
| RF-04 | Administrar la tasa de retención por año.                          |
| RF-05 | Acceder a períodos mensuales.                                       |
| RF-06 | Asociar instituciones participantes a un período.                   |
| RF-07 | Registrar libremente horas sin requerir fecha.                       |
| RF-08 | Editar horas inline.                                                 |
| RF-09 | Eliminar registros de horas.                                         |
| RF-10 | Calcular horas totales automáticamente.                             |
| RF-11 | Calcular ingreso bruto automáticamente.                             |
| RF-12 | Calcular retención según el año automáticamente.                 |
| RF-13 | Calcular líquido estimado.                                          |
| RF-14 | Calcular el consolidado mensual.                                     |
| RF-15 | Consultar períodos históricos.                                     |
| RF-16 | Navegar rápidamente entre meses.                                    |

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

## 15. Pantallas del MVP

El MVP puede organizarse en cinco áreas principales:

1. Dashboard.
2. Mes de trabajo — pantalla fundamental.
3. Instituciones.
4. Tarifas/valores hora.
5. Configuración.

Dashboard inicial: horas trabajadas, bruto acumulado, retención estimada y líquido estimado, además de los resultados por institución.

## 16. Fuera del alcance del MVP

- Instituciones privadas y sus reglas particulares.
- Remuneraciones dependientes y contratos laborales.
- Previsión, Fonasa/Isapre, AFP o licencias médicas.
- Emisión automática de boletas, integración con SII, conciliación bancaria, facturación o pagos efectivamente recibidos.
- Agenda clínica, pacientes, prestaciones médicas, turnos obligatorios o control de asistencia.
- Registro obligatorio por día.

## 17. Métricas futuras

La estructura podrá habilitar posteriormente:

- Ingresos y horas mensuales/anuales.
- Ingreso por institución y porcentaje de ingresos por institución.
- Valor promedio por hora.
- Institución que genera más ingresos.
- Evolución y proyección de ingresos/horas.

Estas métricas no amplían por sí mismas el alcance del MVP.

## 18. Modelo de datos conceptual

El siguiente es un modelo de conceptos, no un esquema físico prescriptivo:

```text
Profesional
  id
  nombre

Institucion
  id
  nombre
  activo

ProfesionalInstitucion
  id
  profesional_id
  institucion_id
  activo

ValorHoraAnual
  id
  profesional_institucion_id
  anio
  valor_hora

Periodo
  id
  profesional_id
  anio
  mes

PeriodoInstitucion
  id
  periodo_id
  profesional_institucion_id

RegistroHora
  id
  periodo_institucion_id
  horas
  orden
  created_at
  updated_at

ConfiguracionTributariaAnual
  id
  anio
  porcentaje_retencion
```

La cardinalidad, claves, precisión, restricciones y estrategia de snapshots/versiones se definen en diseño técnico sin cambiar las reglas funcionales de este PRD.

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

El núcleo del producto es:

```text
Institución + valor hora anual + período mensual + registros libres de horas
                                ↓
                  cálculos y consolidado mensual
```

## 22. Decisiones pendientes

Estas decisiones requieren definición antes de implementar las reglas correspondientes:

1. **Roles:** capacidades concretas de `ADMINISTRADOR` y `PROFESIONAL`, en especial quién administra instituciones/catálogos, tasas tributarias y valores hora.
2. **Horas:** si se admiten fracciones; precisión, unidad mínima, valores válidos y límites de entrada.
3. **Moneda y redondeo:** confirmar moneda de los montos y regla exacta de redondeo de retención y líquido (por registro, institución o total mensual). Los ejemplos no bastan para determinarla.
4. **Histórico:** comportamiento frente a correcciones retroactivas de tasas o valores hora; definir versiones/snapshots, auditoría y recálculo permitido.
5. **Autoguardado:** debounce, feedback, reintentos, edición concurrente y resolución de conflictos entre pestañas/dispositivos.
6. **Orden y reversión:** si el orden de registros es significativo y si se requiere deshacer/restaurar eliminaciones.
7. **Datos de prueba tributarios:** validar tasas anuales antes de considerarlas configuración oficial.

Hasta resolverlas, la IA debe conservarlas como decisiones abiertas y no inventar comportamientos.

## 23. Directrices para agentes de IA

- Tratar este PRD como fuente funcional de verdad para el módulo público.
- No convertir el workspace mensual en un flujo CRUD de formularios/modal por registro.
- No exigir día, turno, semana, jornada ni motivo a los registros de horas.
- No deduplicar filas repetidas ni imponer un límite funcional de cantidad de registros.
- No hardcodear tasas tributarias o valores hora; conservar su año y su contexto profesional-institución.
- No añadir reglas del sector privado ni emitir boletas o pagos reales en el MVP.
- No inventar la matriz de capacidades de roles, precisión de horas, reglas de redondeo o correcciones históricas.
- Aplicar la autorización del backend definida en los estándares; guards y componentes React solo controlan la experiencia visual.
- Mantener los cálculos mostrados actualizados; el backend es la autoridad del resultado persistido.
- Si un requisito técnico parece contradecir una regla de producto, describir el conflicto y pedir decisión antes de cambiar el comportamiento.
