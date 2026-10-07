import { useEffect, useMemo, useState } from 'react'
import { Alert, Card, Empty, Select, Skeleton, Space, Table, Tag, Typography, type TableColumnsType } from 'antd'
import { dashboardService } from '../services/dashboard/dashboardService'
import { professionalService } from '../services/professionals/professionalService'
import { getApiErrorMessage } from '../services/apiClient'
import { formatClp, formatParticipation } from '../utils/format'
import { useAuth } from '../hooks/useAuth'
import { RoleCodes } from '../constants/authorization'
import { IncomeLineChart } from '../components/dashboard/IncomeLineChart'
import type { DashboardData, DashboardMonth, ProfessionalSummary } from '../types/api'

export function DashboardPage() {
  const auth = useAuth()
  const isAdmin = auth.hasRole(RoleCodes.administrator)
  const currentYear = new Date().getFullYear()
  const [fromYear, setFromYear] = useState(currentYear)
  const [toYear, setToYear] = useState(currentYear)
  const [professionalId, setProfessionalId] = useState<string>()
  const [professionals, setProfessionals] = useState<ProfessionalSummary[]>([])
  const [selectedInstitutionKeys, setSelectedInstitutionKeys] = useState<string[]>([])
  const [data, setData] = useState<DashboardData | null>(null)
  const [loading, setLoading] = useState(!isAdmin)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!isAdmin) return
    professionalService.list().then(setProfessionals).catch(requestError => setError(getApiErrorMessage(requestError)))
  }, [isAdmin])

  useEffect(() => {
    if (isAdmin && !professionalId) return
    let active = true
    dashboardService.dashboard({ fromYear, toYear, professionalId: isAdmin ? professionalId : undefined })
      .then(result => { if (active) { setError(null); setData(result) } })
      .catch(requestError => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [fromYear, toYear, professionalId, isAdmin])

  const visibleMonths = useMemo(() => data?.months.filter(month => month.periodExists) ?? [], [data?.months])
  const visibleInstitutions = useMemo(() => {
    if (selectedInstitutionKeys.length === 0) return data?.institutions ?? []
    const selected = new Set(selectedInstitutionKeys)
    return (data?.institutions ?? []).filter(institution => selected.has(institution.key))
  }, [data?.institutions, selectedInstitutionKeys])
  const tableInstitutions = useMemo(() => [...visibleInstitutions].sort((first, second) => {
    if (first.type !== second.type) return first.type === 'public' ? -1 : 1
    return first.name.localeCompare(second.name, 'es-CL')
  }), [visibleInstitutions])
  const visibleMonthsPerYear = useMemo(() => {
    const counts = new Map<number, number>()
    for (const month of visibleMonths)
      counts.set(month.year, (counts.get(month.year) ?? 0) + 1)
    return counts
  }, [visibleMonths])
  const chartData = useMemo(() => data ? { ...data, institutions: visibleInstitutions, months: visibleMonths } : null,
    [data, visibleInstitutions, visibleMonths])

  const columns = useMemo<TableColumnsType<DashboardMonth>>(() => {
    const base: TableColumnsType<DashboardMonth> = [
      {
        title: 'Año', dataIndex: 'year', key: 'year', width: 86, fixed: 'left',
        onCell: (row, index) => ({
          rowSpan: index === undefined || visibleMonths[index - 1]?.year !== row.year
            ? visibleMonthsPerYear.get(row.year) ?? 1
            : 0,
        }),
        render: value => <Typography.Text strong>{value}</Typography.Text>,
      },
      { title: 'Mes', dataIndex: 'month', key: 'month', width: 132, fixed: 'left', render: value => monthName(value) },
    ]
    const institutionColumns: TableColumnsType<DashboardMonth> = tableInstitutions.map(institution => ({
      title: <Space size={4} wrap>
        <Tag color={institution.type === 'private' ? 'purple' : 'blue'}>{institution.type === 'private' ? 'Privada' : 'Pública'}</Tag>
        <span>{institution.name}</span>
      </Space>,
      key: institution.key,
      width: 205,
      render: (_, row) => {
        const amount = row.institutions.find(item => item.institutionKey === institution.key)?.netTotalClp ?? null
        if (amount === null) return <span className="no-data-cell">—</span>
        return <Space className="dashboard-amount-cell" align="center" size={8}>
          <Typography.Text type="secondary">{formatParticipation(amount, row.totalNetClp)}</Typography.Text>
          <Typography.Text strong>{formatClp(amount)}</Typography.Text>
        </Space>
      },
    }))
    return [...base, ...institutionColumns, {
      title: 'Líquido combinado',
      key: 'total',
      width: 175,
      fixed: 'right',
      render: (_, row) => row.totalNetClp === null
        ? <span className="no-data-cell">Sin período</span>
        : <Typography.Text strong className="table-total">{formatClp(row.totalNetClp)}</Typography.Text>,
    }]
  }, [tableInstitutions, visibleMonths, visibleMonthsPerYear])

  const yearOptions = Array.from({ length: 50 }, (_, index) => currentYear + 1 - index)

  return <div className="page-stack">
    <section className="page-heading">
      <div>
        <Typography.Text className="eyebrow">LECTURA HISTÓRICA</Typography.Text>
        <Typography.Title level={1}>La evolución de un vistazo.</Typography.Title>
        <Typography.Paragraph>Compara el líquido por institución y sigue el total combinado mes a mes.</Typography.Paragraph>
      </div>
      <Tag className="dashboard-unit-tag">CLP · importes netos calculados</Tag>
    </section>

    <Card className="filter-card" variant="borderless">
      <div className="dashboard-filters">
        {isAdmin && <label><span>Profesional</span><Select
          showSearch
          optionFilterProp="label"
          placeholder="Selecciona un profesional"
          value={professionalId}
          onChange={value => { setLoading(true); setProfessionalId(value); setSelectedInstitutionKeys([]) }}
          options={professionals.map(person => ({ value: person.id, label: person.name }))}
          style={{ minWidth: 230 }}
        /></label>}
        <label><span>Desde</span><Select value={fromYear} onChange={value => { setLoading(true); setFromYear(value); if (toYear < value) setToYear(value) }} options={yearOptions.map(year => ({ value: year, label: String(year) }))} /></label>
        <label><span>Hasta</span><Select value={toYear} onChange={value => { setLoading(true); setToYear(value) }} options={yearOptions.filter(year => year >= fromYear).map(year => ({ value: year, label: String(year) }))} /></label>
        <label className="institution-filter"><span>Instituciones</span><Select
          mode="multiple"
          allowClear
          placeholder="Todas las instituciones"
          value={selectedInstitutionKeys}
          onChange={setSelectedInstitutionKeys}
          options={(data?.institutions ?? []).map(institution => ({
            value: institution.key,
            label: `${institution.name} · ${institution.type === 'private' ? 'Privada' : 'Pública'}`,
          }))}
          maxTagCount="responsive"
        /></label>
      </div>
    </Card>

    {error && <Alert type="error" showIcon title={error} />}
    {loading ? <Skeleton active paragraph={{ rows: 8 }} /> : isAdmin && !professionalId ? (
      <Card className="empty-workspace" variant="borderless"><Empty description="Selecciona un profesional para consultar su evolución." /></Card>
    ) : data ? <>
      <Card className="dashboard-table-card" variant="borderless">
        <div className="section-card-heading">
          <div><Typography.Text className="eyebrow">DETALLE MENSUAL</Typography.Text><Typography.Title level={3}>Ingresos por institución</Typography.Title></div>
          <Tag variant="filled">{visibleInstitutions.length} instituciones</Tag>
        </div>
        {visibleInstitutions.length === 0 ? <Empty description="No hay instituciones asociadas a este profesional." /> : visibleMonths.length === 0 ? <Empty description="No hay períodos registrados en este rango." /> : <Table<DashboardMonth>
          rowKey={row => `${row.year}-${row.month}`}
          columns={columns}
          dataSource={visibleMonths}
          pagination={false}
          scroll={{ x: 'max-content' }}
          size="middle"
        />}
      </Card>

      <Card className="chart-card" variant="borderless">
        <div className="section-card-heading">
          <div><Typography.Text className="eyebrow">TENDENCIA EN EL TIEMPO</Typography.Text><Typography.Title level={3}>Evolución del líquido mensual</Typography.Title></div>
          <Typography.Text type="secondary">Se muestran únicamente los meses con períodos registrados.</Typography.Text>
        </div>
        <IncomeLineChart data={chartData ?? data} />
      </Card>
    </> : null}
  </div>
}

function monthName(month: number): string {
  const name = new Intl.DateTimeFormat('es-CL', { month: 'long' }).format(new Date(2026, month - 1, 1))
  return `${name.charAt(0).toLocaleUpperCase('es-CL')}${name.slice(1)}`
}
