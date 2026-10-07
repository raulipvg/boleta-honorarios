import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  Alert,
  App as AntdApp,
  Button,
  Card,
  Col,
  Descriptions,
  Empty,
  Form,
  InputNumber,
  Popconfirm,
  Row,
  Select,
  Skeleton,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
  Upload,
  type TableColumnsType,
  type UploadFile,
} from 'antd'
import { DeleteOutlined, DownloadOutlined, FilePdfOutlined, ReloadOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { privateLiquidationService } from '../services/private-liquidations/privateLiquidationService'
import { professionalService } from '../services/professionals/professionalService'
import { profileService } from '../services/profile/profileService'
import { getApiErrorMessage } from '../services/apiClient'
import { useAuth } from '../hooks/useAuth'
import { PermissionCodes, RoleCodes } from '../constants/authorization'
import { formatClp, formatRate } from '../utils/format'
import { monthLabel, monthValue } from '../utils/date'
import { MonthSelector } from '../components/layout/MonthSelector'
import type { PrivateLiquidation, PrivateLiquidationPreview, ProfessionalSummary } from '../types/api'

const MAX_PDF_BYTES = 1_048_576

export function PrivateLiquidationsPage() {
  const { message } = AntdApp.useApp()
  const navigate = useNavigate()
  const auth = useAuth()
  const [searchParams] = useSearchParams()
  const isAdmin = auth.hasRole(RoleCodes.administrator)
  const canCreate = auth.hasPermission(PermissionCodes.privateLiquidationsCreate) && !isAdmin
  const canDelete = auth.hasPermission(PermissionCodes.privateLiquidationsDelete) && !isAdmin
  const [selectedMonth, setSelectedMonth] = useState<Dayjs>(() => {
    const year = Number(searchParams.get('year'))
    const month = Number(searchParams.get('month'))
    return year >= 1900 && month >= 1 && month <= 12 ? dayjs().year(year).month(month - 1).date(1) : dayjs().date(1)
  })
  const [professionals, setProfessionals] = useState<ProfessionalSummary[]>([])
  const [professionalId, setProfessionalId] = useState<string | undefined>(() => searchParams.get('professionalId') ?? undefined)
  const [profileRut, setProfileRut] = useState<string | null>(null)
  const [liquidations, setLiquidations] = useState<PrivateLiquidation[]>([])
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [uploadFileList, setUploadFileList] = useState<UploadFile[]>([])
  const [minutesPerAttention, setMinutesPerAttention] = useState<number | null>(null)
  const [preview, setPreview] = useState<PrivateLiquidationPreview | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { year, month } = monthValue(selectedMonth)

  const loadLiquidations = useCallback((): Promise<PrivateLiquidation[]> => {
    if (isAdmin && !professionalId) {
      return Promise.resolve([])
    }
    return privateLiquidationService.list({
      year,
      month,
      professionalId: isAdmin ? professionalId : undefined,
    })
  }, [isAdmin, month, professionalId, year])

  useEffect(() => {
    if (!isAdmin) {
      profileService.get().then(profile => setProfileRut(profile.rut)).catch(requestError => setError(getApiErrorMessage(requestError)))
      return
    }
    professionalService.list().then(setProfessionals).catch(requestError => setError(getApiErrorMessage(requestError)))
  }, [isAdmin])

  useEffect(() => {
    let active = true
    loadLiquidations()
      .then(rows => { if (active) setLiquidations(rows) })
      .catch(requestError => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [loadLiquidations])

  const previewDocument = async () => {
    if (!selectedFile) {
      setError('Selecciona un archivo PDF.')
      return
    }
    if (selectedFile.size > MAX_PDF_BYTES) {
      setError('El PDF no puede superar 1 MB.')
      return
    }
    if (minutesPerAttention === null || minutesPerAttention <= 0) {
      setError('Ingresa minutos por atención mayores que cero.')
      return
    }

    setBusy(true)
    setError(null)
    setPreview(null)
    try {
      setPreview(await privateLiquidationService.preview(selectedFile, minutesPerAttention))
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'No se pudo interpretar el PDF.'))
    } finally {
      setBusy(false)
    }
  }

  const confirmImport = async () => {
    if (!selectedFile || !preview || minutesPerAttention === null) return
    setBusy(true)
    setError(null)
    try {
      const imported = await privateLiquidationService.import(selectedFile, minutesPerAttention, preview.sha256, preview.appliedRetentionPercentage)
      setPreview(null)
      setSelectedFile(null)
      setUploadFileList([])
      setMinutesPerAttention(null)
      if (imported.year !== year || imported.month !== month) {
        setLoading(true)
        setSelectedMonth(dayjs().year(imported.year).month(imported.month - 1).date(1))
      } else {
        setLiquidations(await loadLiquidations())
      }
      message.success('Liquidación importada y agregada al período mensual.')
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'No se pudo importar la liquidación.'))
    } finally {
      setBusy(false)
    }
  }

  const downloadFile = useCallback(async (liquidation: PrivateLiquidation) => {
    setDownloadingId(liquidation.id)
    try {
      const blob = await privateLiquidationService.download(liquidation.id)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `Liquidacion-${liquidation.liquidationNumber}.pdf`
      link.click()
      URL.revokeObjectURL(url)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'No se pudo descargar el PDF.'))
    } finally {
      setDownloadingId(null)
    }
  }, [])

  const deleteLiquidation = useCallback(async (liquidation: PrivateLiquidation) => {
    try {
      await privateLiquidationService.delete(liquidation.id)
      setLiquidations(await loadLiquidations())
      message.success('Se eliminó el PDF y se actualizaron los totales del mes.')
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'No se pudo eliminar la liquidación.'))
    }
  }, [loadLiquidations, message])

  const columns = useMemo<TableColumnsType<PrivateLiquidation>>(() => [
    { title: 'Quincena', dataIndex: 'fortnight', key: 'fortnight', width: 100, render: value => `${value}.ª` },
    { title: 'Liquidación', dataIndex: 'liquidationNumber', key: 'number', width: 125 },
    { title: 'Fecha', dataIndex: 'liquidationDate', key: 'date', width: 115, render: value => dayjs(value).format('DD-MM-YYYY') },
    { title: 'Entidad pagadora', key: 'payer', width: 250, render: (_, row) => <Space direction="vertical" size={0}><span>{row.payerLegalName}</span><Typography.Text type="secondary">RUT {row.payerRut}</Typography.Text></Space> },
    { title: 'RUT cobrador', dataIndex: 'collectorRut', key: 'collector-rut', width: 125 },
    { title: 'Servicio', dataIndex: 'paymentService', key: 'service', width: 170 },
    { title: 'Ejecutor', dataIndex: 'executorName', key: 'executor', width: 190 },
    { title: 'Total servicio', dataIndex: 'serviceTotalClp', key: 'service-total', align: 'right', width: 130, render: value => value === null ? '—' : formatClp(value) },
    { title: 'Bruto', dataIndex: 'grossTotalClp', key: 'gross', align: 'right', width: 130, render: formatClp },
    { title: 'Tasa', dataIndex: 'appliedRetentionPercentage', key: 'retention-rate', align: 'right', width: 90, render: value => formatRate(value) },
    { title: 'Retención', dataIndex: 'retentionTotalClp', key: 'retention', align: 'right', width: 130, render: formatClp },
    { title: 'Líquido', dataIndex: 'netTotalClp', key: 'net', align: 'right', width: 130, render: value => <Typography.Text strong>{formatClp(value)}</Typography.Text> },
    { title: 'Atenciones', dataIndex: 'attentionCount', key: 'attentions', align: 'right', width: 105 },
    { title: 'Cierre PDF', dataIndex: 'reportedAttentionCount', key: 'reported-attentions', align: 'right', width: 110, render: value => value ?? '—' },
    { title: 'Min/atención', dataIndex: 'minutesPerAttention', key: 'minutes-per-attention', align: 'right', width: 125, render: value => `${formatMinutes(value)} min` },
    { title: 'Minutos totales', dataIndex: 'totalAttentionMinutes', key: 'minutes-total', align: 'right', width: 135, render: value => `${formatMinutes(value)} min` },
    {
      title: 'Archivo', key: 'actions', fixed: 'right', width: 110,
      render: (_, row) => <Space>
        <Tooltip title="Descargar PDF"><Button type="text" icon={<DownloadOutlined />} loading={downloadingId === row.id} onClick={() => void downloadFile(row)} aria-label="Descargar PDF" /></Tooltip>
        {canDelete && <Popconfirm title="Eliminar esta liquidación y sus totales" okText="Eliminar" cancelText="Cancelar" onConfirm={() => void deleteLiquidation(row)}>
          <Tooltip title="Eliminar liquidación"><Button type="text" danger icon={<DeleteOutlined />} aria-label="Eliminar liquidación" /></Tooltip>
        </Popconfirm>}
      </Space>,
    },
  ], [canDelete, deleteLiquidation, downloadFile, downloadingId])

  const privateGross = liquidations.reduce((total, item) => total + item.grossTotalClp, 0)
  const privateRetention = liquidations.reduce((total, item) => total + item.retentionTotalClp, 0)
  const privateNet = liquidations.reduce((total, item) => total + item.netTotalClp, 0)
  const attentionCount = liquidations.reduce((total, item) => total + item.attentionCount, 0)
  const totalAttentionMinutes = liquidations.reduce((total, item) => total + item.totalAttentionMinutes, 0)

  return <div className="page-stack">
    <section className="page-heading">
      <div>
        <Typography.Text className="eyebrow">INGRESOS PRIVADOS</Typography.Text>
        <Typography.Title level={1}>Liquidaciones de Sanatorio Alemán.</Typography.Title>
        <Typography.Paragraph>Importa cada PDF y conserva el cálculo del líquido por documento.</Typography.Paragraph>
      </div>
      <Tag color="purple">Participaciones · CLP</Tag>
    </section>

    {isAdmin && <Card className="filter-card" variant="borderless">
      <Space wrap>
        <Typography.Text strong>Profesional</Typography.Text>
        <Select
          showSearch
          optionFilterProp="label"
          placeholder="Selecciona un profesional"
          value={professionalId}
          onChange={value => { setLoading(Boolean(value)); setProfessionalId(value) }}
          options={professionals.map(person => ({ value: person.id, label: person.name }))}
          style={{ minWidth: 300 }}
        />
        <Tag color="gold">Consulta y descarga</Tag>
      </Space>
    </Card>}

    {error && <Alert type="error" showIcon title={error} closable={{ onClose: () => setError(null) }} />}
    {isAdmin && !professionalId ? <Card className="empty-workspace" variant="borderless"><Empty description="Selecciona un profesional para consultar sus liquidaciones." /></Card> : <>
      {canCreate && !profileRut && <Alert
        type="warning"
        showIcon
        title="Completa tu RUT profesional antes de importar una liquidación."
        action={<Button type="link" onClick={() => navigate('/settings')}>Ir a configuración</Button>}
      />}

      {canCreate && <Card className="add-institution-card" variant="borderless">
        <div className="section-card-heading">
          <div><Typography.Text className="eyebrow">NUEVA LIQUIDACIÓN</Typography.Text><Typography.Title level={3}>Analizar PDF</Typography.Title></div>
          <Tag color="blue">Máximo 1 MB</Tag>
        </div>
        <Form layout="vertical" requiredMark={false}>
          <Row gutter={[16, 0]} align="bottom">
            <Col xs={24} lg={14}>
              <Form.Item label="Archivo PDF" required>
                <Upload.Dragger
                  accept="application/pdf,.pdf"
                  maxCount={1}
                  fileList={uploadFileList}
                  beforeUpload={file => {
                    if (file.size > MAX_PDF_BYTES) {
                      setSelectedFile(null)
                      setUploadFileList([])
                      setPreview(null)
                      setError('El PDF no puede superar 1 MB.')
                      return Upload.LIST_IGNORE
                    }
                    setError(null)
                    setSelectedFile(file)
                    setPreview(null)
                    setUploadFileList([{ uid: file.uid, name: file.name, status: 'done', originFileObj: file }])
                    return false
                  }}
                  onRemove={() => { setSelectedFile(null); setPreview(null); setUploadFileList([]); return true }}
                  disabled={busy || !profileRut}
                  multiple={false}
                >
                  <p className="ant-upload-drag-icon"><FilePdfOutlined /></p>
                  <p className="ant-upload-text">Selecciona o arrastra la liquidación</p>
                  <p className="ant-upload-hint">Se procesan los servicios de la plantilla de Sanatorio Alemán.</p>
                </Upload.Dragger>
              </Form.Item>
            </Col>
            <Col xs={24} lg={6}>
              <Form.Item label="Minutos por atención" required>
                <Space.Compact style={{ width: '100%' }}>
                  <InputNumber
                    min={0}
                    step={1}
                    precision={6}
                    value={minutesPerAttention ?? undefined}
                    onChange={value => { setMinutesPerAttention(typeof value === 'number' ? value : null); setPreview(null) }}
                    style={{ width: 'calc(100% - 56px)' }}
                    disabled={busy}
                  />
                  <Space.Addon>min</Space.Addon>
                </Space.Compact>
              </Form.Item>
            </Col>
            <Col xs={24} lg={4}>
              <Form.Item>
                <Button type="primary" icon={<ReloadOutlined />} block disabled={!profileRut || !selectedFile || minutesPerAttention === null || minutesPerAttention <= 0} loading={busy} onClick={() => void previewDocument()}>
                  Analizar
                </Button>
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>}

      {preview && <Card className="institution-card" variant="borderless">
        <div className="section-card-heading">
          <div><Typography.Text className="eyebrow">PREVISUALIZACIÓN</Typography.Text><Typography.Title level={3}>Revisa los datos leídos</Typography.Title></div>
          <Tag color="green">RUT cobrador validado</Tag>
        </div>
        <Descriptions bordered size="small" column={{ xs: 1, sm: 2, lg: 3 }}>
          <Descriptions.Item label="Período">{monthLabel(preview.year, preview.month)} · {preview.fortnight}.ª quincena</Descriptions.Item>
          <Descriptions.Item label="N.º liquidación">{preview.liquidationNumber}</Descriptions.Item>
          <Descriptions.Item label="Fecha liquidación">{dayjs(preview.liquidationDate).format('DD-MM-YYYY')}</Descriptions.Item>
          <Descriptions.Item label="Entidad pagadora">{preview.payerLegalName} · RUT {preview.payerRut}</Descriptions.Item>
          <Descriptions.Item label="RUT cobrador">{preview.collectorRut}</Descriptions.Item>
          <Descriptions.Item label="Servicio">{preview.paymentService}</Descriptions.Item>
          <Descriptions.Item label="Ejecutor">{preview.executorName || '—'}</Descriptions.Item>
          <Descriptions.Item label="Total servicio">{preview.serviceTotalClp === null ? '—' : formatClp(preview.serviceTotalClp)}</Descriptions.Item>
          <Descriptions.Item label="Atenciones">{preview.attentionCount}</Descriptions.Item>
          <Descriptions.Item label="Bruto">{formatClp(preview.grossTotalClp)}</Descriptions.Item>
          <Descriptions.Item label={`Retención (${formatRate(preview.appliedRetentionPercentage)})`}>{formatClp(preview.retentionTotalClp)}</Descriptions.Item>
          <Descriptions.Item label="Líquido calculado"><Typography.Text strong>{formatClp(preview.netTotalClp)}</Typography.Text></Descriptions.Item>
          <Descriptions.Item label="Minutos por atención">{formatMinutes(preview.minutesPerAttention)} min</Descriptions.Item>
          <Descriptions.Item label="Atenciones informadas">{preview.reportedAttentionCount ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Minutos totales">{formatMinutes(preview.totalAttentionMinutes)} min</Descriptions.Item>
        </Descriptions>
        <Row justify="end" style={{ marginTop: 16 }}>
          <Space>
            <Button disabled={busy} onClick={() => setPreview(null)}>Volver</Button>
            <Button type="primary" loading={busy} onClick={() => void confirmImport()}>Confirmar importación</Button>
          </Space>
        </Row>
      </Card>}

      <Card className="dashboard-table-card" variant="borderless">
        <div className="section-card-heading">
          <div><Typography.Text className="eyebrow">{monthLabel(year, month).toUpperCase()}</Typography.Text><Typography.Title level={3}>Liquidaciones importadas</Typography.Title></div>
          <MonthSelector
            value={selectedMonth}
            onChange={value => { setLoading(true); setPreview(null); setSelectedMonth(value.date(1)) }}
          />
        </div>
        {loading ? <Skeleton active paragraph={{ rows: 4 }} /> : liquidations.length === 0 ? <Empty description="No hay liquidaciones privadas para este mes." /> : <>
          <Space wrap className="month-summary-metrics">
            <Tag color="purple">Bruto privado: {formatClp(privateGross)}</Tag>
            <Tag color="orange">Retención privada: {formatClp(privateRetention)}</Tag>
            <Tag color="green">Líquido privado: {formatClp(privateNet)}</Tag>
            <Tag>{attentionCount} atenciones</Tag>
            <Tag>{formatMinutes(totalAttentionMinutes)} minutos</Tag>
          </Space>
          <Table<PrivateLiquidation>
            rowKey="id"
            columns={columns}
            dataSource={liquidations}
            pagination={{ pageSize: 10, showSizeChanger: false }}
            scroll={{ x: 'max-content' }}
            size="middle"
          />
        </>}
      </Card>
    </>}
  </div>
}

function formatMinutes(value: number): string {
  return new Intl.NumberFormat('es-CL', { maximumFractionDigits: 6 }).format(value)
}
