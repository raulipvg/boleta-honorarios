import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  Alert,
  App as AntdApp,
  Button,
  Card,
  Descriptions,
  DatePicker,
  Empty,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Skeleton,
  Spin,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
  Upload,
  type TableColumnsType,
  type UploadFile,
} from 'antd'
import { DeleteOutlined, DownloadOutlined, EyeOutlined, FilePdfOutlined, MailOutlined, PlusOutlined } from '@ant-design/icons'
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
type PrivateImportInstitution = 'sanatorio-aleman' | 'centro-cebien'

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
  const [selectedImportInstitution, setSelectedImportInstitution] = useState<PrivateImportInstitution | null>(null)
  const [emailBody, setEmailBody] = useState('')
  const [cebienAccountingMonth, setCebienAccountingMonth] = useState<Dayjs | null>(null)
  const [minutesPerAttention, setMinutesPerAttention] = useState<number | null>(null)
  const [preview, setPreview] = useState<PrivateLiquidationPreview | null>(null)
  const [importModalOpen, setImportModalOpen] = useState(false)
  const [importError, setImportError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)
  const [previewingId, setPreviewingId] = useState<string | null>(null)
  const [previewLiquidation, setPreviewLiquidation] = useState<PrivateLiquidation | null>(null)
  const [previewPdfUrl, setPreviewPdfUrl] = useState<string | null>(null)
  const [emailSourceText, setEmailSourceText] = useState<string | null>(null)
  const [readingEmailSourceId, setReadingEmailSourceId] = useState<string | null>(null)
  const [previewModalOpen, setPreviewModalOpen] = useState(false)
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

  useEffect(() => {
    if (!previewPdfUrl) return
    return () => URL.revokeObjectURL(previewPdfUrl)
  }, [previewPdfUrl])

  const resetImportDraft = () => {
    setSelectedImportInstitution(null)
    setEmailBody('')
    setCebienAccountingMonth(null)
    setPreview(null)
    setSelectedFile(null)
    setUploadFileList([])
    setMinutesPerAttention(null)
    setImportError(null)
  }

  const closeImportModal = () => {
    if (busy) return
    setImportModalOpen(false)
    resetImportDraft()
  }

  const previewDocument = async () => {
    if (!selectedImportInstitution) {
      setImportError('Selecciona una institución privada.')
      return
    }
    if (minutesPerAttention === null || !Number.isInteger(minutesPerAttention) || minutesPerAttention < 1) {
      setImportError('Ingresa un número entero de minutos por atención mayor que cero.')
      return
    }

    if (selectedImportInstitution === 'sanatorio-aleman') {
      if (!profileRut) {
        setImportError('Completa y valida tu RUT en el perfil antes de importar liquidaciones de Sanatorio Alemán.')
        return
      }
      if (!selectedFile) {
        setImportError('Selecciona un archivo PDF.')
        return
      }
      if (selectedFile.size > MAX_PDF_BYTES) {
        setImportError('El PDF no puede superar 1 MB.')
        return
      }
    } else {
      if (!emailBody.trim()) {
        setImportError('Pega el cuerpo del correo de Centro Cebien.')
        return
      }
      if (!cebienAccountingMonth) {
        setImportError('Selecciona el Mes contable.')
        return
      }
    }

    const loadingKey = 'private-liquidation-analysis'
    setBusy(true)
    setImportError(null)
    setPreview(null)
    message.open({
      key: loadingKey,
      type: 'loading',
      content: selectedImportInstitution === 'centro-cebien' ? 'Analizando correo…' : 'Analizando liquidación…',
      duration: 0,
    })
    try {
      const parsed = selectedImportInstitution === 'centro-cebien'
        ? await privateLiquidationService.previewCebien(
          emailBody, monthValue(cebienAccountingMonth!).year, monthValue(cebienAccountingMonth!).month, minutesPerAttention)
        : await privateLiquidationService.preview(selectedFile!, minutesPerAttention)
      setPreview(parsed)
      message.destroy(loadingKey)
      message.success('Datos analizados. Revisa la previsualización antes de importar.')
    } catch (requestError) {
      setImportError(getApiErrorMessage(requestError,
        selectedImportInstitution === 'centro-cebien' ? 'No se pudo interpretar el correo.' : 'No se pudo interpretar el PDF.'))
    } finally {
      message.destroy(loadingKey)
      setBusy(false)
    }
  }

  const confirmImport = async () => {
    if (!preview || minutesPerAttention === null || !Number.isInteger(minutesPerAttention) || minutesPerAttention < 1) return
    if (preview.sourceType === 'pdf' && !selectedFile) return
    if (preview.sourceType === 'email' && (!emailBody.trim() || !cebienAccountingMonth)) return
    const loadingKey = 'private-liquidation-import'
    setBusy(true)
    setImportError(null)
    message.open({ key: loadingKey, type: 'loading', content: 'Importando liquidación…', duration: 0 })
    try {
      const imported = preview.sourceType === 'email'
        ? await privateLiquidationService.importCebien(
          emailBody, monthValue(cebienAccountingMonth!).year, monthValue(cebienAccountingMonth!).month,
          minutesPerAttention, preview.sha256, preview.appliedRetentionPercentage)
        : await privateLiquidationService.import(
          selectedFile!, minutesPerAttention, preview.sha256, preview.appliedRetentionPercentage)
      message.destroy(loadingKey)
      setImportModalOpen(false)
      resetImportDraft()
      if (imported.accountingYear !== year || imported.accountingMonth !== month) {
        setLoading(true)
        setSelectedMonth(dayjs().year(imported.accountingYear).month(imported.accountingMonth - 1).date(1))
      } else {
        try {
          setLiquidations(await loadLiquidations())
        } catch (requestError) {
          setError(getApiErrorMessage(requestError, 'La liquidación se importó, pero no se pudo actualizar el listado.'))
        }
      }
      message.success('Liquidación importada y agregada al período mensual.')
    } catch (requestError) {
      setImportError(getApiErrorMessage(requestError, 'No se pudo importar la liquidación.'))
    } finally {
      message.destroy(loadingKey)
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

  const previewFile = useCallback(async (liquidation: PrivateLiquidation) => {
    if (previewingId !== null || readingEmailSourceId !== null) return
    setPreviewingId(liquidation.id)
    try {
      const blob = await privateLiquidationService.download(liquidation.id)
      setPreviewLiquidation(liquidation)
      setPreviewPdfUrl(URL.createObjectURL(blob))
      setPreviewModalOpen(true)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'No se pudo abrir la vista previa del PDF.'))
    } finally {
      setPreviewingId(null)
    }
  }, [previewingId, readingEmailSourceId])

  const previewEmailSource = useCallback(async (liquidation: PrivateLiquidation) => {
    if (previewingId !== null || readingEmailSourceId !== null) return
    setReadingEmailSourceId(liquidation.id)
    try {
      const source = await privateLiquidationService.source(liquidation.id)
      setPreviewLiquidation(liquidation)
      setPreviewPdfUrl(null)
      setEmailSourceText(source)
      setPreviewModalOpen(true)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'No se pudo abrir el correo original.'))
    } finally {
      setReadingEmailSourceId(null)
    }
  }, [previewingId, readingEmailSourceId])

  const closePreviewModal = () => {
    setPreviewModalOpen(false)
    setPreviewLiquidation(null)
    setPreviewPdfUrl(null)
    setEmailSourceText(null)
  }

  const deleteLiquidation = useCallback(async (liquidation: PrivateLiquidation) => {
    try {
      await privateLiquidationService.delete(liquidation.id)
      setLiquidations(await loadLiquidations())
      message.success('Se eliminó la liquidación y se actualizaron los totales del mes.')
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'No se pudo eliminar la liquidación.'))
    }
  }, [loadLiquidations, message])

  const columns = useMemo<TableColumnsType<PrivateLiquidation>>(() => [
    { title: 'Quincena', dataIndex: 'fortnight', key: 'fortnight', width: 100, render: value => value === null ? '—' : `${value}.ª` },
    { title: 'Liquidación', dataIndex: 'liquidationNumber', key: 'number', width: 125, render: value => value ?? '—' },
    { title: 'Fecha', dataIndex: 'liquidationDate', key: 'date', width: 115, render: value => value ? dayjs(value).format('DD-MM-YYYY') : '—' },
    { title: 'Entidad pagadora', key: 'payer', width: 250, render: (_, row) => <Space orientation="vertical" size={0}><span>{row.payerLegalName}</span><Typography.Text type="secondary">RUT {row.payerRut}</Typography.Text></Space> },
    { title: 'RUT cobrador', dataIndex: 'collectorRut', key: 'collector-rut', width: 125, render: value => value ?? '—' },
    { title: 'Servicio', dataIndex: 'paymentService', key: 'service', width: 170 },
    { title: 'Ejecutor', dataIndex: 'executorName', key: 'executor', width: 190, render: value => value ?? '—' },
    { title: 'Total servicio', dataIndex: 'serviceTotalClp', key: 'service-total', align: 'right', width: 130, render: value => value === null ? '—' : formatClp(value) },
    { title: 'Bruto', dataIndex: 'grossTotalClp', key: 'gross', align: 'right', width: 130, render: formatClp },
    { title: 'Tasa', dataIndex: 'appliedRetentionPercentage', key: 'retention-rate', align: 'right', width: 90, render: value => formatRate(value) },
    { title: 'Retención', dataIndex: 'retentionTotalClp', key: 'retention', align: 'right', width: 130, render: formatClp },
    { title: 'Líquido', dataIndex: 'netTotalClp', key: 'net', align: 'right', width: 130, render: value => <Typography.Text strong>{formatClp(value)}</Typography.Text> },
    { title: 'Atenciones', dataIndex: 'attentionCount', key: 'attentions', align: 'right', width: 105 },
    { title: 'Cierre PDF', dataIndex: 'reportedAttentionCount', key: 'reported-attentions', align: 'right', width: 110, render: value => value ?? '—' },
    { title: 'Min/atención', dataIndex: 'minutesPerAttention', key: 'minutes-per-attention', align: 'right', width: 125, render: value => `${formatMinutes(value)} min` },
    { title: 'Hrs totales', dataIndex: 'totalAttentionMinutes', key: 'hours-total', align: 'right', width: 135, render: value => `${formatHoursFromMinutes(value)} h` },
    {
      title: 'Acciones', key: 'actions', fixed: 'right', width: 145,
      render: (_, row) => <Space>
        {row.sourceType === 'pdf' ? <>
          <Tooltip title="Ver PDF"><Button type="text" icon={<EyeOutlined />} loading={previewingId === row.id} disabled={(previewingId !== null && previewingId !== row.id) || readingEmailSourceId !== null} onClick={() => void previewFile(row)} aria-label="Ver PDF" /></Tooltip>
          <Tooltip title="Descargar PDF"><Button type="text" icon={<DownloadOutlined />} loading={downloadingId === row.id} onClick={() => void downloadFile(row)} aria-label="Descargar PDF" /></Tooltip>
        </> : <Tooltip title="Ver correo original"><Button type="text" icon={<MailOutlined />} loading={readingEmailSourceId === row.id} disabled={(readingEmailSourceId !== null && readingEmailSourceId !== row.id) || previewingId !== null} onClick={() => void previewEmailSource(row)} aria-label="Ver correo original" /></Tooltip>}
        {canDelete && <Popconfirm title="Eliminar esta liquidación y sus totales" okText="Eliminar" cancelText="Cancelar" onConfirm={() => void deleteLiquidation(row)}>
          <Tooltip title="Eliminar liquidación"><Button type="text" danger icon={<DeleteOutlined />} aria-label="Eliminar liquidación" /></Tooltip>
        </Popconfirm>}
      </Space>,
    },
  ], [canDelete, deleteLiquidation, downloadFile, downloadingId, previewEmailSource, previewFile, previewingId, readingEmailSourceId])

  const privateGross = liquidations.reduce((total, item) => total + item.grossTotalClp, 0)
  const privateRetention = liquidations.reduce((total, item) => total + item.retentionTotalClp, 0)
  const privateNet = liquidations.reduce((total, item) => total + item.netTotalClp, 0)
  const attentionCount = liquidations.reduce((total, item) => total + item.attentionCount, 0)
  const totalAttentionMinutes = liquidations.reduce((total, item) => total + item.totalAttentionMinutes, 0)

  return <div className="page-stack">
    <section className="page-heading">
      <div>
        <Typography.Text className="eyebrow">INGRESOS PRIVADOS</Typography.Text>
        <Typography.Title level={1}>Liquidaciones privadas.</Typography.Title>
        <Typography.Paragraph>Importa y revisa los documentos de tus instituciones privadas.</Typography.Paragraph>
      </div>
      <Space>
        <Tag color="purple">Instituciones privadas · CLP</Tag>
        {canCreate && <Button type="primary" icon={<PlusOutlined />} onClick={() => {
          resetImportDraft()
          setImportModalOpen(true)
        }}>Nueva liquidación</Button>}
      </Space>
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
      <Card className="dashboard-table-card" variant="borderless">
        <div className="section-card-heading">
          <div><Typography.Text className="eyebrow">{monthLabel(year, month).toUpperCase()}</Typography.Text><Typography.Title level={3}>Liquidaciones importadas</Typography.Title></div>
          <MonthSelector
            value={selectedMonth}
            onChange={value => { setLoading(true); setPreview(null); setSelectedMonth(value.date(1)) }}
          />
        </div>
        {loading ? <Skeleton active paragraph={{ rows: 4 }} /> : liquidations.length === 0 ? <Empty description="No hay liquidaciones privadas para este mes." /> : <>
          <Table<PrivateLiquidation>
            rowKey="id"
            columns={columns}
            dataSource={liquidations}
            pagination={{ pageSize: 10, showSizeChanger: false }}
            scroll={{ x: 'max-content' }}
            size="middle"
            summary={() => <Table.Summary fixed="bottom">
              <Table.Summary.Row>
                <Table.Summary.Cell index={0} colSpan={7} align="right">
                  <Typography.Text strong>Totales del mes</Typography.Text>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={7} align="right">—</Table.Summary.Cell>
                <Table.Summary.Cell index={8} align="right"><Typography.Text strong>{formatClp(privateGross)}</Typography.Text></Table.Summary.Cell>
                <Table.Summary.Cell index={9} align="right">—</Table.Summary.Cell>
                <Table.Summary.Cell index={10} align="right"><Typography.Text strong>{formatClp(privateRetention)}</Typography.Text></Table.Summary.Cell>
                <Table.Summary.Cell index={11} align="right"><Typography.Text strong>{formatClp(privateNet)}</Typography.Text></Table.Summary.Cell>
                <Table.Summary.Cell index={12} align="right"><Typography.Text strong>{attentionCount}</Typography.Text></Table.Summary.Cell>
                <Table.Summary.Cell index={13} align="right">—</Table.Summary.Cell>
                <Table.Summary.Cell index={14} align="right">—</Table.Summary.Cell>
                <Table.Summary.Cell index={15} align="right"><Typography.Text strong>{formatHoursFromMinutes(totalAttentionMinutes)} h</Typography.Text></Table.Summary.Cell>
                <Table.Summary.Cell index={16} align="right">—</Table.Summary.Cell>
              </Table.Summary.Row>
            </Table.Summary>}
          />
        </>}
      </Card>
    </>}

    <Modal
      title={previewLiquidation?.sourceType === 'email'
        ? `Correo original · ${previewLiquidation.paymentService}`
        : `Vista previa · Liquidación ${previewLiquidation?.liquidationNumber ?? ''}`}
      open={previewModalOpen}
      onCancel={closePreviewModal}
      footer={null}
      destroyOnHidden
      width={960}
    >
      {previewLiquidation?.sourceType === 'email'
        ? emailSourceText !== null
          ? <pre style={{ maxHeight: '75vh', overflow: 'auto', whiteSpace: 'pre-wrap', margin: 0 }}>{emailSourceText}</pre>
          : <Spin />
        : previewPdfUrl
        ? <iframe
          title={`Vista previa de la liquidación ${previewLiquidation?.liquidationNumber ?? ''}`}
          src={previewPdfUrl}
          style={{ width: '100%', height: '75vh', border: 0 }}
        />
        : <Spin />}
    </Modal>

    {canCreate && <Modal
      title={<Space>{selectedImportInstitution === 'centro-cebien' ? <MailOutlined /> : <FilePdfOutlined />} Nueva liquidación privada</Space>}
      open={importModalOpen}
      onCancel={closeImportModal}
      onOk={() => { if (preview) void confirmImport(); else void previewDocument() }}
      cancelText="Cancelar"
      confirmLoading={busy}
      okText={preview ? 'Confirmar importación' : selectedImportInstitution === 'centro-cebien' ? 'Analizar correo' : 'Analizar PDF'}
      okButtonProps={{ disabled: busy || (preview
        ? (preview.sourceType === 'pdf'
          ? !selectedFile
          : !emailBody.trim() || cebienAccountingMonth === null)
        : (minutesPerAttention === null || !Number.isInteger(minutesPerAttention) || minutesPerAttention < 1
          || (selectedImportInstitution === 'sanatorio-aleman' && (!selectedFile || !profileRut))
          || (selectedImportInstitution === 'centro-cebien' && (!emailBody.trim() || cebienAccountingMonth === null))
          || selectedImportInstitution === null)) }}
      cancelButtonProps={{ disabled: busy }}
      closable={!busy}
      mask={{ closable: !busy }}
      keyboard={!busy}
      destroyOnHidden
      width={1200}
    >
      {importError && <Alert type="error" showIcon title={importError} closable={{ onClose: () => setImportError(null) }} style={{ marginBottom: 16 }} />}
      <Form layout="vertical" requiredMark={false}>
        <Form.Item label="Institución privada" required>
          <Select
            value={selectedImportInstitution ?? undefined}
            placeholder="Selecciona una institución"
            disabled={busy}
            onChange={value => {
              setSelectedImportInstitution(value)
              setSelectedFile(null)
              setUploadFileList([])
              setEmailBody('')
              setCebienAccountingMonth(null)
              setMinutesPerAttention(null)
              setPreview(null)
              setImportError(null)
            }}
            options={[
              { value: 'sanatorio-aleman', label: 'Sanatorio Alemán' },
              { value: 'centro-cebien', label: 'Centro Cebien' },
            ]}
          />
        </Form.Item>

        {selectedImportInstitution === 'sanatorio-aleman' && <>
          {!profileRut && <Alert
            type="warning"
            showIcon
            title="Completa tu RUT profesional antes de importar una liquidación de Sanatorio Alemán."
            action={<Button type="link" onClick={() => navigate('/settings')}>Ir a configuración</Button>}
            style={{ marginBottom: 16 }}
          />}
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
                setImportError('El PDF no puede superar 1 MB.')
                return Upload.LIST_IGNORE
              }
              setImportError(null)
              setSelectedFile(file)
              setPreview(null)
              setUploadFileList([{ uid: file.uid, name: file.name, status: 'done', originFileObj: file }])
              return false
            }}
            onRemove={() => {
              setSelectedFile(null)
              setPreview(null)
              setUploadFileList([])
              setImportError(null)
              return true
            }}
            disabled={busy || !profileRut}
            multiple={false}
          >
            <p className="ant-upload-drag-icon"><FilePdfOutlined /></p>
            <p className="ant-upload-text">Selecciona o arrastra la liquidación</p>
            <p className="ant-upload-hint">PDF de Sanatorio Alemán · Máximo 1 MB</p>
          </Upload.Dragger>
          </Form.Item>
        </>}

        {selectedImportInstitution === 'centro-cebien' && <>
          <Form.Item label="Cuerpo del correo de Centro Cebien" required>
            <Input.TextArea
              value={emailBody}
              onChange={event => { setEmailBody(event.target.value); setPreview(null); setImportError(null) }}
              autoSize={{ minRows: 8, maxRows: 16 }}
              disabled={busy}
              placeholder="Pega aquí el cuerpo del correo enviado por Centro Cebien."
            />
          </Form.Item>
          <Form.Item label="Mes contable" required>
            <DatePicker
              picker="month"
              value={cebienAccountingMonth}
              onChange={value => { setCebienAccountingMonth(value?.date(1) ?? null); setPreview(null); setImportError(null) }}
              format={(value: Dayjs) => monthLabel(value.year(), value.month() + 1)}
              placeholder="Selecciona el mes contable"
              disabled={busy}
              style={{ width: '100%' }}
            />
          </Form.Item>
        </>}

        {selectedImportInstitution !== null && <Form.Item label="Minutos enteros por atención" required>
          <Space.Compact style={{ width: '100%' }}>
            <InputNumber
              min={1}
              step={1}
              precision={0}
              value={minutesPerAttention ?? undefined}
              onChange={value => {
                setMinutesPerAttention(typeof value === 'number' && Number.isInteger(value) ? value : null)
                setPreview(null)
                setImportError(null)
              }}
              style={{ width: 'calc(100% - 56px)' }}
              disabled={busy}
            />
            <Space.Addon>min</Space.Addon>
          </Space.Compact>
        </Form.Item>}
      </Form>
      {preview && <>
        <div className="section-card-heading">
          <div><Typography.Text className="eyebrow">PREVISUALIZACIÓN</Typography.Text><Typography.Title level={4}>Revisa los datos leídos</Typography.Title></div>
          <Tag color="green">{preview.sourceType === 'pdf' ? 'RUT cobrador validado' : 'Profesional validado'}</Tag>
        </div>
        <Descriptions bordered size="small" column={{ xs: 1, sm: 2, lg: 3 }}>
          <Descriptions.Item label={preview.sourceType === 'pdf' ? 'Período del PDF' : 'Mes de atención'}>
            {monthLabel(preview.serviceYear, preview.serviceMonth)}{preview.fortnight === null ? '' : ` · ${preview.fortnight}.ª quincena`}
          </Descriptions.Item>
          <Descriptions.Item label="Mes contable">{monthLabel(preview.accountingYear, preview.accountingMonth)}</Descriptions.Item>
          <Descriptions.Item label="N.º liquidación">{preview.liquidationNumber ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Fecha liquidación">{preview.liquidationDate ? dayjs(preview.liquidationDate).format('DD-MM-YYYY') : '—'}</Descriptions.Item>
          <Descriptions.Item label="Entidad pagadora">{preview.payerLegalName} · RUT {preview.payerRut}</Descriptions.Item>
          <Descriptions.Item label="RUT cobrador">{preview.collectorRut ?? '—'}</Descriptions.Item>
          {preview.reportedProfessionalName && <Descriptions.Item label="Profesional informado">{preview.reportedProfessionalName}</Descriptions.Item>}
          <Descriptions.Item label="Servicio">{preview.paymentService}</Descriptions.Item>
          <Descriptions.Item label="Ejecutor">{preview.executorName || '—'}</Descriptions.Item>
          <Descriptions.Item label="Total servicio">{preview.serviceTotalClp === null ? '—' : formatClp(preview.serviceTotalClp)}</Descriptions.Item>
          <Descriptions.Item label="Atenciones">{preview.attentionCount}</Descriptions.Item>
          {preview.attentionCountsByService.length > 0 && <Descriptions.Item label="Atenciones por prestación">
            {preview.attentionCountsByService.map(item => `${item.serviceName}: ${item.count}`).join(' · ')}
          </Descriptions.Item>}
          <Descriptions.Item label="Bruto">{formatClp(preview.grossTotalClp)}</Descriptions.Item>
          <Descriptions.Item label={`Retención (${formatRate(preview.appliedRetentionPercentage)})`}>{formatClp(preview.retentionTotalClp)}</Descriptions.Item>
          <Descriptions.Item label="Líquido calculado"><Typography.Text strong>{formatClp(preview.netTotalClp)}</Typography.Text></Descriptions.Item>
          <Descriptions.Item label="Minutos por atención">{formatMinutes(preview.minutesPerAttention)} min</Descriptions.Item>
          <Descriptions.Item label="Atenciones informadas">{preview.reportedAttentionCount ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Minutos totales">{formatMinutes(preview.totalAttentionMinutes)} min</Descriptions.Item>
        </Descriptions>
        <Space style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
          <Button disabled={busy} onClick={() => setPreview(null)}>Volver a modificar</Button>
        </Space>
      </>}
    </Modal>}
  </div>
}

function formatMinutes(value: number): string {
  return new Intl.NumberFormat('es-CL', { maximumFractionDigits: 0 }).format(value)
}

function formatHoursFromMinutes(minutes: number): string {
  return new Intl.NumberFormat('es-CL', { maximumFractionDigits: 2 }).format(minutes / 60)
}
