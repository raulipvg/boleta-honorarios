import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import { CloseOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Col, DatePicker, Empty, Input, Popconfirm, Row, Select, Skeleton, Space, Tag, Typography, type InputRef } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { incomeService } from '../services/incomeService'
import { getApiErrorMessage } from '../services/apiClient'
import { formatClp, formatRate } from '../utils/format'
import { monthLabel, monthValue } from '../utils/date'
import { useAuth } from '../hooks/useAuth'
import { PermissionCodes, RoleCodes } from '../constants/authorization'
import type { HourRecord, MonthlyWorkspace, ProfessionalInstitution, ProfessionalSummary, SavedHourRecord } from '../types/api'

export function MonthWorkspacePage() {
  const auth = useAuth()
  const isAdmin = auth.hasRole(RoleCodes.administrator)
  const canEdit = auth.hasPermission(PermissionCodes.periodsManage) && auth.hasPermission(PermissionCodes.hoursManage)
  const [selectedMonth, setSelectedMonth] = useState<Dayjs>(dayjs().date(1))
  const [professionals, setProfessionals] = useState<ProfessionalSummary[]>([])
  const [professionalId, setProfessionalId] = useState<string>()
  const [relationships, setRelationships] = useState<ProfessionalInstitution[]>([])
  const [workspace, setWorkspace] = useState<MonthlyWorkspace | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [addDraft, setAddDraft] = useState(false)
  const [institutionToAdd, setInstitutionToAdd] = useState<string>()
  const [draftRows, setDraftRows] = useState<{ institutionId: string; key: number }[]>([])
  const [loadingOlderInstitutionId, setLoadingOlderInstitutionId] = useState<string | null>(null)
  const [savedAt, setSavedAt] = useState<number | null>(null)
  const draftSequence = useRef(0)
  const { year, month } = monthValue(selectedMonth)
  const markSaved = () => setSavedAt(value => (value ?? 0) + 1)

  const loadWorkspace = useCallback(async () => {
    if (isAdmin && !professionalId) return null
    const [nextWorkspace, nextRelations] = await Promise.all([
      incomeService.workspace(year, month, isAdmin ? professionalId : undefined),
      incomeService.professionalInstitutions(isAdmin ? professionalId : undefined),
    ])
    return { workspace: nextWorkspace, relationships: nextRelations }
  }, [isAdmin, professionalId, year, month])

  useEffect(() => {
    if (!isAdmin) return
    incomeService.professionals().then(setProfessionals).catch(requestError => setError(getApiErrorMessage(requestError)))
  }, [isAdmin])

  useEffect(() => {
    let active = true
    loadWorkspace()
      .then(result => {
        if (!active) return
        setWorkspace(result?.workspace ?? null)
        setRelationships(result?.relationships ?? [])
      })
      .catch(requestError => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [loadWorkspace])

  const availableRelationships = useMemo(() => {
    const linked = new Set(workspace?.institutions.map(x => x.professionalInstitutionId) ?? [])
    return relationships.filter(relation => relation.active && !linked.has(relation.id))
  }, [relationships, workspace])

  const persistPeriodInstitution = async () => {
    if (!institutionToAdd) return
    setBusy(true)
    setError(null)
    try {
      const result = await incomeService.addInstitutionToPeriod(year, month, institutionToAdd)
      setWorkspace(result)
      setAddDraft(false)
      setInstitutionToAdd(undefined)
      markSaved()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const removePeriodInstitution = async (institutionId: string) => {
    setBusy(true)
    try {
      setWorkspace(await incomeService.removeInstitutionFromPeriod(year, month, institutionId))
      markSaved()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const reloadCurrentWorkspace = async () => {
    const result = await loadWorkspace()
    if (result) {
      setWorkspace(result.workspace)
      setRelationships(result.relationships)
    }
  }

  const commitHour = (relationId: string, result: SavedHourRecord) => {
    setWorkspace(current => mergeWorkspace(current, result.workspace, result.record, relationId))
    markSaved()
  }

  const loadOlderHours = async (institution: MonthlyWorkspace['institutions'][number]) => {
    if (!workspace || institution.nextBeforeOrder === null || loadingOlderInstitutionId) return
    setLoadingOlderInstitutionId(institution.professionalInstitutionId)
    try {
      const page = await incomeService.olderHours(
        year,
        month,
        institution.professionalInstitutionId,
        institution.nextBeforeOrder,
        isAdmin ? professionalId : undefined,
      )
      setWorkspace(current => {
        if (!current) return current
        return {
          ...current,
          institutions: current.institutions.map(item => item.professionalInstitutionId !== institution.professionalInstitutionId
            ? item
            : {
              ...item,
              records: mergeRecords(item.records, page.records),
              hasMoreRecords: page.hasMoreRecords,
              nextBeforeOrder: page.nextBeforeOrder,
            }),
        }
      })
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setLoadingOlderInstitutionId(null)
    }
  }

  const addNextDraft = (institutionId: string) => {
    draftSequence.current += 1
    setDraftRows(rows => [...rows, { institutionId, key: draftSequence.current }])
  }

  const removeDraft = (key: number) => setDraftRows(rows => rows.filter(row => row.key !== key))

  const setCurrentMonth = (direction: number) => {
    setLoading(true)
    setError(null)
    setDraftRows([])
    setSelectedMonth(value => value.add(direction, 'month'))
  }

  const activeProfessional = professionals.find(x => x.id === professionalId)

  return (
    <div className="page-stack">
      <section className="page-heading month-heading">
        <div>
          <Typography.Text className="eyebrow">ESPACIO DE TRABAJO</Typography.Text>
          <Typography.Title level={1}>Tus horas, en contexto.</Typography.Title>
          <Typography.Paragraph>Registra cada bloque con libertad. Los totales se actualizan al guardar.</Typography.Paragraph>
        </div>
        <div className="month-switcher">
          <Button aria-label="Mes anterior" onClick={() => setCurrentMonth(-1)}>‹</Button>
          <DatePicker
            picker="month"
            allowClear={false}
            value={selectedMonth}
            onChange={value => {
              if (!value) return
              setLoading(true)
              setError(null)
              setDraftRows([])
              setSelectedMonth(value.date(1))
            }}
            format="MMMM YYYY"
            inputReadOnly
          />
          <Button aria-label="Mes siguiente" onClick={() => setCurrentMonth(1)}>›</Button>
        </div>
      </section>

      {isAdmin && <Card className="filter-card" bordered={false}>
        <Space wrap>
          <Typography.Text strong>Profesional</Typography.Text>
          <Select
            showSearch
            optionFilterProp="label"
            placeholder="Selecciona un profesional para consultar"
            value={professionalId}
            onChange={value => { setLoading(true); setDraftRows([]); setProfessionalId(value) }}
            options={professionals.map(person => ({ value: person.id, label: person.name }))}
            style={{ minWidth: 300 }}
          />
          <Tag color="gold">Consulta de solo lectura</Tag>
        </Space>
      </Card>}

      {error && <Alert type="error" showIcon message={error} closable onClose={() => setError(null)} />}
      {loading ? <Skeleton active paragraph={{ rows: 5 }} /> : !workspace ? (
        <Card className="empty-workspace" bordered={false}><Empty description="Selecciona un profesional para consultar su período." /></Card>
      ) : (
        <>
          <section className="month-summary-card">
            <div className="month-summary-heading">
              <div>
                <Typography.Text className="eyebrow">{monthLabel(year, month).toUpperCase()}</Typography.Text>
                <Typography.Title level={2}>{isAdmin ? activeProfessional?.name ?? 'Período profesional' : 'Resumen del mes'}</Typography.Title>
              </div>
              <div className="save-feedback" aria-live="polite">
                {busy ? <><span className="saving-pulse" /> Guardando…</> : savedAt ? <><span className="saved-check">✓</span> Guardado</> : 'Todos los cambios se confirman en el servidor'}
              </div>
            </div>
            <div className="summary-metrics">
              <SummaryMetric label="Horas totales" value={`${workspace.totalHours} h`} />
              <SummaryMetric label="Bruto" value={formatClp(workspace.grossTotalClp)} />
              <SummaryMetric label="Retención" value={formatClp(workspace.retentionTotalClp)} hint={workspace.appliedRetentionPercentage === null ? undefined : formatRate(workspace.appliedRetentionPercentage)} />
              <SummaryMetric label="Líquido estimado" value={formatClp(workspace.netTotalClp)} emphasis />
            </div>
          </section>

          {isAdmin && <Alert className="readonly-alert" type="info" showIcon message="Vista de administrador" description="Puedes consultar períodos, horas y montos. Las modificaciones de horas corresponden exclusivamente al profesional." />}

          {canEdit && <Card className="add-institution-card" bordered={false}>
            {!addDraft ? <Button type="dashed" onClick={() => setAddDraft(true)} disabled={availableRelationships.length === 0}>
              ＋ Agregar institución a este mes
            </Button> : <Space wrap>
              <Select
                showSearch
                optionFilterProp="label"
                placeholder="Institución con tarifa configurada para este año"
                value={institutionToAdd}
                onChange={setInstitutionToAdd}
                style={{ minWidth: 300 }}
                options={availableRelationships.map(relation => {
                  const rate = relation.rates.find(item => item.year === year)
                  return { value: relation.id, label: rate ? `${relation.institutionName} · ${formatClp(rate.hourlyRateClp)}/h` : `${relation.institutionName} · tarifa pendiente`, disabled: !rate }
                })}
              />
              <Button type="primary" loading={busy} disabled={!institutionToAdd} onClick={() => void persistPeriodInstitution()}>Añadir al mes</Button>
              <Button onClick={() => { setAddDraft(false); setInstitutionToAdd(undefined) }}>Cancelar</Button>
            </Space>}
            {availableRelationships.length === 0 && <Typography.Text type="secondary">Configura una institución y su tarifa anual para poder agregarla a este mes.</Typography.Text>}
          </Card>}

          {workspace.institutions.length === 0 && <Card className="empty-period" bordered={false}>
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={workspace.exists ? 'Todavía no agregas instituciones a este mes.' : 'Este período aún no tiene actividad.'} />
          </Card>}

          <Row gutter={[18, 18]}>
            {workspace.institutions.map((institution, index) => (
              <Col key={institution.professionalInstitutionId}>
                <Card className="institution-card" bordered={false}>
                  <div className="institution-card-top">
                    <div className="institution-title-wrap">
                      <span className={`institution-index index-${index % 4}`}>{String(index + 1).padStart(2, '0')}</span>
                      <div>
                        <Typography.Title level={3}>{institution.institutionName}</Typography.Title>
                        <Typography.Text type="secondary">{formatClp(institution.hourlyRateClp)} / hora</Typography.Text>
                      </div>
                    </div>
                    {canEdit && <Popconfirm
                      title="Retirar institución del período"
                      description="Solo se puede retirar si no tiene registros de horas."
                      okText="Retirar"
                      cancelText="Cancelar"
                      onConfirm={() => removePeriodInstitution(institution.professionalInstitutionId)}
                    ><Button type="text" danger disabled={busy}>Retirar</Button></Popconfirm>}
                  </div>

                  <div className="hour-entry-list" data-institution={institution.professionalInstitutionId}>
                    {institution.records.map(record => <HourEditor
                      key={record.id}
                      record={record}
                      canEdit={canEdit}
                      onCreate={hours => incomeService.addHours(year, month, institution.professionalInstitutionId, hours)}
                      onUpdate={(id, hours, version) => incomeService.updateHours(id, hours, version)}
                      onDelete={record => deleteRecord(institution.professionalInstitutionId, record)}
                      onReload={reloadCurrentWorkspace}
                      onCommitted={result => commitHour(institution.professionalInstitutionId, result)}
                      onEnterNext={() => addNextDraft(institution.professionalInstitutionId)}
                    />)}
                    {draftRows.filter(row => row.institutionId === institution.professionalInstitutionId).map(draft => <HourEditor
                      key={`draft-${draft.key}`}
                      draftKey={draft.key}
                      canEdit={canEdit}
                      onCreate={hours => incomeService.addHours(year, month, institution.professionalInstitutionId, hours)}
                      onUpdate={(id, hours, version) => incomeService.updateHours(id, hours, version)}
                      onDelete={record => deleteRecord(institution.professionalInstitutionId, record)}
                      onReload={reloadCurrentWorkspace}
                      onCommitted={result => commitHour(institution.professionalInstitutionId, result)}
                      onEnterNext={() => addNextDraft(institution.professionalInstitutionId)}
                      onCancel={() => removeDraft(draft.key)}
                      onPersisted={() => removeDraft(draft.key)}
                    />)}
                    {institution.recordsCount > 0 && <Typography.Text className="records-count">
                      Mostrando {institution.records.length} de {institution.recordsCount} registros
                    </Typography.Text>}
                    {institution.hasMoreRecords && <Button
                      type="link"
                      size="small"
                      loading={loadingOlderInstitutionId === institution.professionalInstitutionId}
                      disabled={loadingOlderInstitutionId !== null && loadingOlderInstitutionId !== institution.professionalInstitutionId}
                      onClick={() => void loadOlderHours(institution)}
                    >Cargar registros anteriores</Button>}
                    {institution.records.length === 0 && !draftRows.some(row => row.institutionId === institution.professionalInstitutionId) && <div className="empty-hour-hint">Agrega el primer registro de horas.</div>}
                  </div>

                  {canEdit && <Button type="dashed" block className="add-hour-button" disabled={busy}
                    onClick={() => addNextDraft(institution.professionalInstitutionId)}>＋ Agregar horas</Button>}

                  <div className="institution-totals">
                    <div><span>Horas</span><strong>{institution.totalHours} h</strong></div>
                    <div><span>Bruto</span><strong>{formatClp(institution.grossTotalClp)}</strong></div>
                    <div><span>Retención</span><strong>{formatClp(institution.retentionTotalClp)}</strong></div>
                    <div className="net-line"><span>Líquido est.</span><strong>{formatClp(institution.netTotalClp)}</strong></div>
                  </div>
                </Card>
              </Col>
            ))}
          </Row>
        </>
      )}
    </div>
  )

  async function deleteRecord(relationId: string, record: HourRecord): Promise<MonthlyWorkspace> {
    const updated = await incomeService.deleteHours(record.id, record.version)
    setWorkspace(current => mergeWorkspace(current, updated, undefined, undefined, relationId, record.id))
    markSaved()
    return updated
  }
}

function mergeRecords(...lists: HourRecord[][]): HourRecord[] {
  const records = new Map<string, HourRecord>()
  for (const list of lists)
    for (const record of list) records.set(record.id, record)
  return [...records.values()].sort((left, right) => left.order - right.order)
}

function mergeWorkspace(
  current: MonthlyWorkspace | null,
  server: MonthlyWorkspace,
  savedRecord?: HourRecord,
  savedRelationId?: string,
  removedRelationId?: string,
  removedRecordId?: string,
): MonthlyWorkspace {
  if (!current || current.professionalId !== server.professionalId || current.year !== server.year || current.month !== server.month)
    return server

  return {
    ...server,
    institutions: server.institutions.map(serverInstitution => {
      const loaded = current.institutions.find(item => item.professionalInstitutionId === serverInstitution.professionalInstitutionId)
      const recordsById = new Map(mergeRecords(loaded?.records ?? [], serverInstitution.records).map(record => [record.id, record]))
      if (savedRecord && savedRelationId === serverInstitution.professionalInstitutionId)
        recordsById.set(savedRecord.id, savedRecord)
      if (removedRecordId && removedRelationId === serverInstitution.professionalInstitutionId)
        recordsById.delete(removedRecordId)
      const records = [...recordsById.values()].sort((left, right) => left.order - right.order)
      return {
        ...serverInstitution,
        records,
        hasMoreRecords: loaded ? loaded.hasMoreRecords : serverInstitution.hasMoreRecords,
        nextBeforeOrder: loaded ? loaded.nextBeforeOrder : serverInstitution.nextBeforeOrder,
      }
    }),
  }
}

function SummaryMetric({ label, value, hint, emphasis = false }: { label: string; value: string; hint?: string; emphasis?: boolean }) {
  return <div className={`summary-metric ${emphasis ? 'summary-metric-emphasis' : ''}`}>
    <Typography.Text>{label}</Typography.Text>
    <Typography.Title level={3}>{value}</Typography.Title>
    {hint && <Tag bordered={false}>{hint}</Tag>}
  </div>
}

function HourEditor({
  record,
  draftKey,
  canEdit,
  onCreate,
  onUpdate,
  onDelete,
  onReload,
  onCommitted,
  onEnterNext,
  onCancel,
  onPersisted,
}: {
  record?: HourRecord
  draftKey?: number
  canEdit: boolean
  onCreate: (hours: number) => Promise<SavedHourRecord>
  onUpdate: (id: string, hours: number, version: number) => Promise<SavedHourRecord>
  onDelete: (record: HourRecord) => Promise<MonthlyWorkspace>
  onReload: () => Promise<void>
  onCommitted: (result: SavedHourRecord) => void
  onEnterNext: () => void
  onCancel?: () => void
  onPersisted?: () => void
}) {
  const [editedValue, setEditedValue] = useState<string | null>(null)
  const [state, setState] = useState<'idle' | 'pending' | 'saving' | 'saved' | 'error' | 'conflict'>('idle')
  const [error, setError] = useState<string | null>(null)
  const [deleting, setDeleting] = useState(false)
  const inFlight = useRef(false)
  const inputRef = useRef<InputRef>(null)
  const value = editedValue ?? (record ? String(record.hours) : '')
  const isDraft = record === undefined
  const isDirty = editedValue !== null && editedValue !== (record ? String(record.hours) : '')

  useEffect(() => {
    if (isDraft) requestAnimationFrame(() => inputRef.current?.focus())
  }, [isDraft])

  const save = async (createNext: boolean) => {
    if (!canEdit || inFlight.current) return
    const normalized = value.trim()
    if (!normalized) {
      if (isDraft) setState('idle')
      return
    }
    if (!/^\d+$/.test(normalized) || Number(normalized) < 1 || Number(normalized) > 2_147_483_647) {
      setError('Ingresa horas enteras desde 1.')
      setState('error')
      return
    }
    const hours = Number(normalized)
    if (record && record.hours === hours && !isDirty) {
      setState('saved')
      if (createNext) onEnterNext()
      return
    }

    if (createNext) onEnterNext()
    inFlight.current = true
    setState('saving')
    setError(null)
    const wasDraft = isDraft
    try {
      const result = record
        ? await onUpdate(record.id, hours, record.version)
        : await onCreate(hours)
      setEditedValue(null)
      onCommitted(result)
      setState('saved')
      if (wasDraft && draftKey !== undefined) onPersisted?.()
    } catch (requestError) {
      const statusCode = (requestError as { response?: { status?: number } }).response?.status
      setState(statusCode === 409 ? 'conflict' : 'error')
      setError(statusCode === 409
        ? 'El registro cambió en otra sesión. Se recargaron los datos; reaplica tu valor si sigue siendo correcto.'
        : getApiErrorMessage(requestError, 'No se pudo guardar el registro.'))
      if (statusCode === 409) await onReload()
    } finally {
      inFlight.current = false
    }
  }

  const keyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.preventDefault()
      void save(true)
    } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      const rows = [...(event.currentTarget.closest('.hour-entry-list')?.querySelectorAll<HTMLInputElement>('[data-hour-editor="true"]') ?? [])]
      const position = rows.indexOf(event.currentTarget)
      const next = rows[position + (event.key === 'ArrowDown' ? 1 : -1)]
      if (next) {
        event.preventDefault()
        next.focus()
        next.select()
      }
    }
  }

  const statusLabel = state === 'saving' ? 'Guardando…' : state === 'saved' ? 'Guardado' : state === 'conflict' ? 'Conflicto' : state === 'error' ? 'Error al guardar' : null

  return <div className={`hour-editor-row ${isDraft ? 'hour-editor-draft' : ''}`}>
    <div className="hour-editor-wrap">
      <Input
        ref={inputRef}
        data-hour-editor="true"
        aria-label="Horas trabajadas"
        inputMode="numeric"
        value={value}
        disabled={!canEdit || deleting}
        placeholder={isDraft ? 'Horas' : undefined}
        onChange={event => {
          setEditedValue(event.target.value)
          setState('pending')
          setError(null)
        }}
        onBlur={() => { if (isDirty || isDraft) void save(false) }}
        onKeyDown={keyDown}
        suffix={<span className="hour-unit">h</span>}
      />
      {record && canEdit && <Popconfirm
        title="Eliminar registro"
        description="Se actualizarán los totales del mes."
        okText="Eliminar"
        cancelText="Cancelar"
        onConfirm={async () => {
          setDeleting(true)
          try { await onDelete(record) } catch (deleteError) { setError(getApiErrorMessage(deleteError)); setState('error') } finally { setDeleting(false) }
        }}
      ><Button type="text" danger size="large" icon={<CloseOutlined />} disabled={deleting || state === 'pending' || state === 'saving'} aria-label="Eliminar registro" /></Popconfirm>}
      {!record && canEdit && onCancel && <Button type="text" size="small" onClick={onCancel} aria-label="Cancelar fila">×</Button>}
    </div>
    <div className="hour-editor-feedback" aria-live="polite">
      {statusLabel && <Typography.Text className={`save-state state-${state}`}>{statusLabel}</Typography.Text>}
      {state === 'conflict' && <Button size="small" type="link" onClick={() => void save(false)}>Reaplicar</Button>}
      {error && state !== 'conflict' && <Typography.Text type="danger" className="inline-error">{error}</Typography.Text>}
    </div>
  </div>
}
