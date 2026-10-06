import { useCallback, useEffect, useMemo, useState } from 'react'
import { CheckOutlined, CloseOutlined, DisconnectOutlined, LinkOutlined, PlusOutlined } from '@ant-design/icons'
import { Alert, App as AntdApp, Button, Card, Col, Empty, Flex, Form, Input, InputNumber, Row, Select, Skeleton, Space, Tag, Tooltip, Typography } from 'antd'
import type { InputNumberProps } from 'antd'
import { incomeService } from '../services/incomeService'
import { getApiErrorMessage } from '../services/apiClient'
import { useAuth } from '../hooks/useAuth'
import { PermissionCodes, RoleCodes } from '../constants/authorization'
import { formatClp } from '../utils/format'
import type { Institution, ProfessionalInstitution, ProfessionalSummary } from '../types/api'

function HourlyRateInput(props: InputNumberProps) {
  return <Space.Compact className="rate-hourly-input-compact">
    <InputNumber {...props} className="rate-hourly-input" />
    <Space.Addon>CLP</Space.Addon>
  </Space.Compact>
}

export function InstitutionsRatesPage() {
  const { message } = AntdApp.useApp()
  const auth = useAuth()
  const isAdmin = auth.hasRole(RoleCodes.administrator)
  const canCreateRelation = auth.hasPermission(PermissionCodes.relationshipsManage)
  const canCreateRate = auth.hasPermission(PermissionCodes.ratesCreate) && !isAdmin
  const [professionals, setProfessionals] = useState<ProfessionalSummary[]>([])
  const [professionalId, setProfessionalId] = useState<string>()
  const [catalog, setCatalog] = useState<Institution[]>([])
  const [relations, setRelations] = useState<ProfessionalInstitution[]>([])
  const [institutionId, setInstitutionId] = useState<string>()
  const [quickCreateOpen, setQuickCreateOpen] = useState(false)
  const [newInstitutionName, setNewInstitutionName] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const reload = useCallback(async () => {
    const relationPromise = isAdmin && !professionalId
      ? Promise.resolve([] as ProfessionalInstitution[])
      : incomeService.professionalInstitutions(isAdmin ? professionalId : undefined)
    const [institutionOptions, professionalRelations] = await Promise.all([incomeService.institutions(), relationPromise])
    setCatalog(institutionOptions)
    setRelations(professionalRelations)
  }, [isAdmin, professionalId])

  useEffect(() => {
    if (!isAdmin) return
    incomeService.professionals().then(setProfessionals).catch(requestError => setError(getApiErrorMessage(requestError)))
  }, [isAdmin])

  useEffect(() => {
    let active = true
    const relationPromise = isAdmin && !professionalId
      ? Promise.resolve([] as ProfessionalInstitution[])
      : incomeService.professionalInstitutions(isAdmin ? professionalId : undefined)
    Promise.all([incomeService.institutions(), relationPromise])
      .then(([institutionOptions, professionalRelations]) => {
        if (!active) return
        setCatalog(institutionOptions)
        setRelations(professionalRelations)
      })
      .catch(requestError => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [isAdmin, professionalId])

  const availableCatalog = useMemo(() => {
    const linked = new Set(relations.map(relation => relation.publicInstitutionId))
    return catalog.filter(institution => institution.active && !linked.has(institution.id))
  }, [catalog, relations])

  const addRelation = async () => {
    if (!institutionId) return
    setBusy(true)
    setError(null)
    try {
      await incomeService.addProfessionalInstitution(institutionId)
      setInstitutionId(undefined)
      await reload()
      message.success('Institución asociada a tu perfil.')
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally { setBusy(false) }
  }

  const quickCreateInstitution = async () => {
    const name = newInstitutionName.trim()
    if (!name) return
    setBusy(true)
    setError(null)
    try {
      await incomeService.createAndAddProfessionalInstitution(name)
      setNewInstitutionName('')
      setQuickCreateOpen(false)
      await reload()
      message.success('Institución creada o reutilizada y asociada a tu perfil.')
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally { setBusy(false) }
  }

  const addRate = async (relationId: string, values: { year: number; hourlyRateClp: number }) => {
    setBusy(true)
    setError(null)
    try {
      await incomeService.addRate(relationId, values.year, values.hourlyRateClp)
      await reload()
      message.success('Nueva versión de tarifa publicada. Los períodos existentes conservan su valor aplicado.')
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally { setBusy(false) }
  }

  const deactivate = async (relationId: string) => {
    setBusy(true)
    try {
      await incomeService.deactivateProfessionalInstitution(relationId)
      await reload()
      message.success('Relación desactivada. El historial mensual se conserva.')
    } catch (requestError) { setError(getApiErrorMessage(requestError)) }
    finally { setBusy(false) }
  }

  return <div className="page-stack">
    <section className="page-heading">
      <div><Typography.Text className="eyebrow">CONFIGURACIÓN DE TRABAJO</Typography.Text><Typography.Title level={1}>Instituciones y tarifas.</Typography.Title>
        <Typography.Paragraph>Define dónde trabajas y conserva las versiones de cada valor hora.</Typography.Paragraph></div>
      <Tag color="cyan">Tarifas privadas por profesional</Tag>
    </section>

    {isAdmin && <Card className="filter-card" variant="borderless">
      <Space wrap><Typography.Text strong>Profesional</Typography.Text><Select
        showSearch optionFilterProp="label" placeholder="Selecciona un profesional para consultar"
        value={professionalId} onChange={value => { setLoading(Boolean(value)); setProfessionalId(value) }}
        options={professionals.map(person => ({ value: person.id, label: person.name }))} style={{ minWidth: 320 }}
      /><Tag color="gold">Consulta de solo lectura para tarifas</Tag></Space>
    </Card>}
    {isAdmin && <Alert type="info" showIcon title="Las cuentas con rol Administrador pueden consultar tarifas, pero nunca crear ni versionarlas." />}
    {error && <Alert type="error" showIcon title={error} closable={{ onClose: () => setError(null) }} />}

    {canCreateRelation && <Card className="add-institution-card" variant="borderless">
      <Flex justify="space-between" align="center" gap={16} wrap className="institution-setup-header">
        <div className="section-card-heading"><div><Typography.Text className="eyebrow">TU RED DE TRABAJO</Typography.Text><Typography.Title level={3}>Agregar una institución</Typography.Title></div></div>
        <Space wrap className="institution-quick-create-row">
          <Select showSearch optionFilterProp="label" placeholder="Selecciona una institución pública"
            value={institutionId} onChange={setInstitutionId} style={{ width: 320, maxWidth: '100%', minWidth: 0 }}
            options={availableCatalog.map(item => ({ value: item.id, label: item.name }))} />
          <Tooltip title="Asociar a mi perfil">
            <Button type="primary" icon={<LinkOutlined />} aria-label="Asociar a mi perfil" disabled={!institutionId} loading={busy} onClick={() => void addRelation()} />
          </Tooltip>
          <Tooltip title={quickCreateOpen ? 'Cancelar nueva institución' : 'Crear nueva institución'}>
            <Button
              icon={quickCreateOpen ? <CloseOutlined /> : <PlusOutlined />}
              aria-label={quickCreateOpen ? 'Cancelar nueva institución' : 'Crear nueva institución'}
              danger={quickCreateOpen}
              disabled={busy}
              onClick={() => { setQuickCreateOpen(open => !open); setNewInstitutionName('') }}
            />
          </Tooltip>
        </Space>
      </Flex>
      {quickCreateOpen && <Flex justify="flex-end" align="center" gap={8} wrap className="institution-quick-create-form">
        <Input autoFocus aria-label="Nombre de la institución" disabled={busy} maxLength={200} value={newInstitutionName} placeholder="Nombre de la institución"
          onChange={event => setNewInstitutionName(event.target.value)}
          onPressEnter={() => void quickCreateInstitution()} style={{ width: 320, maxWidth: '100%' }} />
        <Tooltip title="Crear y asociar">
          <Button type="primary" icon={<CheckOutlined />} aria-label="Crear y asociar" disabled={!newInstitutionName.trim()} loading={busy} onClick={() => void quickCreateInstitution()} />
        </Tooltip>
      </Flex>}
    </Card>}

    {loading ? <Skeleton active paragraph={{ rows: 5 }} /> : isAdmin && !professionalId ? (
      <Card className="empty-workspace" variant="borderless"><Empty description="Selecciona un profesional para consultar sus relaciones y tarifas." /></Card>
    ) : relations.length === 0 ? (
      <Card className="empty-workspace" variant="borderless"><Empty description="Todavía no hay instituciones asociadas." /></Card>
    ) : <Row gutter={[16, 16]}>
      {relations.map((relation, index) => <Col key={relation.id} xs={24} md={12}>
        <Card className="relation-card" variant="borderless">
          <div className="relation-card-header">
            <div className="institution-title-wrap"><span className={`institution-index index-${index % 4}`}>{String(index + 1).padStart(2, '0')}</span>
              <div><Typography.Title level={3}>{relation.institutionName}</Typography.Title><Tag color={relation.active ? 'green' : 'default'}>{relation.active ? 'Activa' : 'Inactiva'}</Tag></div>
            </div>
            {canCreateRelation && relation.active && <Tooltip title="Desactivar relación">
              <Button type="text" danger icon={<DisconnectOutlined />} aria-label="Desactivar relación" disabled={busy} onClick={() => void deactivate(relation.id)} />
            </Tooltip>}
          </div>

          <div className="rate-history">
            <Typography.Text className="eyebrow">HISTORIAL DE VALOR HORA</Typography.Text>
            {relation.rates.length === 0 ? <Typography.Text type="secondary">Aún no se han definido tarifas.</Typography.Text> : <div className="rate-version-grid">
              {relation.rates.map(rate => <div className="rate-version" key={`${rate.year}-${rate.version}`}>
                <span>{rate.year} · v{rate.version}</span><strong>{formatClp(rate.hourlyRateClp)}<small>/h</small></strong>
              </div>)}
            </div>}
          </div>

          {canCreateRate && relation.active && <Form layout="inline" className="rate-create-form" onFinish={values => addRate(relation.id, values)}>
            <Form.Item name="year" label="Año" rules={[{ required: true, message: 'Ingresa el año de la tarifa.' }]}>
              <InputNumber className="rate-year-input" min={1900} max={32767} precision={0} placeholder="Año" />
            </Form.Item>
            <Form.Item name="hourlyRateClp" label="Tarifa bruta por hora" rules={[{ required: true, message: 'Ingresa el valor hora.' }]}>
              <HourlyRateInput min={0} max={9_007_199_254_740_991} precision={0} step={100} placeholder="0" />
            </Form.Item>
            <Form.Item><Button type="primary" htmlType="submit" loading={busy}>Publicar nueva versión</Button></Form.Item>
          </Form>}
        </Card>
      </Col>)}
    </Row>}
  </div>
}
