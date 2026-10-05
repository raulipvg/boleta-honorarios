import { useEffect, useMemo, useState } from 'react'
import { Alert, Card, Empty, Select, Skeleton, Table, Tag, Typography, type TableColumnsType } from 'antd'
import { incomeService } from '../services/incomeService'
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
  const [availableInstitutions, setAvailableInstitutions] = useState<{ id: string; name: string }[]>([])
  const [institutionIds, setInstitutionIds] = useState<string[]>([])
  const [data, setData] = useState<DashboardData | null>(null)
  const [loading, setLoading] = useState(!isAdmin)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!isAdmin) return
    incomeService.professionals().then(setProfessionals).catch(requestError => setError(getApiErrorMessage(requestError)))
  }, [isAdmin])

  useEffect(() => {
    if (isAdmin && !professionalId) {
      return
    }
    incomeService.professionalInstitutions(isAdmin ? professionalId : undefined)
      .then(relations => setAvailableInstitutions(relations.map(relation => ({ id: relation.publicInstitutionId, name: relation.institutionName }))))
      .catch(requestError => setError(getApiErrorMessage(requestError)))
  }, [isAdmin, professionalId])

  useEffect(() => {
    if (isAdmin && !professionalId) {
      return
    }
    let active = true
    incomeService.dashboard({ fromYear, toYear, professionalId: isAdmin ? professionalId : undefined, institutionIds })
      .then(result => { if (active) { setError(null); setData(result) } })
      .catch(requestError => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [fromYear, toYear, professionalId, institutionIds, isAdmin])

  const columns = useMemo<TableColumnsType<DashboardMonth>>(() => {
    const base: TableColumnsType<DashboardMonth> = [
      {
        title: 'Año', dataIndex: 'year', key: 'year', width: 86, fixed: 'left',
        onCell: (row, index) => ({ rowSpan: index === undefined || data?.months[index - 1]?.year !== row.year ? 12 : 0 }),
        render: value => <Typography.Text strong>{value}</Typography.Text>,
      },
      { title: 'Mes', dataIndex: 'month', key: 'month', width: 132, fixed: 'left', render: value => monthName(value) },
    ]
    const institutionColumns: TableColumnsType<DashboardMonth> = (data?.institutions ?? []).map(institution => ({
      title: institution.name,
      key: institution.id,
      width: 205,
      render: (_, row) => {
        const amount = row.institutions.find(item => item.institutionId === institution.id)?.netTotalClp ?? null
        if (amount === null) return <span className="no-data-cell">—</span>
        return <div className="dashboard-amount-cell"><strong>{formatClp(amount)}</strong><span>{formatParticipation(amount, row.totalNetClp)}</span></div>
      },
    }))
    return [...base, ...institutionColumns, {
      title: 'Total líquido',
      key: 'total',
      width: 170,
      fixed: 'right',
      render: (_, row) => row.totalNetClp === null
        ? <span className="no-data-cell">Sin período</span>
        : <Typography.Text strong className="table-total">{formatClp(row.totalNetClp)}</Typography.Text>,
    }]
  }, [data?.institutions, data?.months])

  const yearOptions = Array.from({ length: 50 }, (_, index) => currentYear + 1 - index)

  return <div className="page-stack">
    <section className="page-heading">
      <div>
        <Typography.Text className="eyebrow">LECTURA HISTÓRICA</Typography.Text>
        <Typography.Title level={1}>La evolución de un vistazo.</Typography.Title>
        <Typography.Paragraph>Compara el líquido estimado por institución y sigue el total mes a mes.</Typography.Paragraph>
      </div>
      <Tag className="dashboard-unit-tag">CLP · valores líquidos estimados</Tag>
    </section>

    <Card className="filter-card" bordered={false}>
      <div className="dashboard-filters">
        {isAdmin && <label><span>Profesional</span><Select
          showSearch
          optionFilterProp="label"
          placeholder="Selecciona un profesional"
          value={professionalId}
          onChange={value => {
            setLoading(Boolean(value))
            setProfessionalId(value)
            setInstitutionIds([])
            setAvailableInstitutions([])
          }}
          options={professionals.map(person => ({ value: person.id, label: person.name }))}
          style={{ minWidth: 230 }}
        /></label>}
        <label><span>Desde</span><Select value={fromYear} onChange={value => { setLoading(true); setFromYear(value); if (toYear < value) setToYear(value) }} options={yearOptions.map(year => ({ value: year, label: String(year) }))} /></label>
        <label><span>Hasta</span><Select value={toYear} onChange={value => { setLoading(true); setToYear(value) }} options={yearOptions.filter(year => year >= fromYear).map(year => ({ value: year, label: String(year) }))} /></label>
        <label className="institution-filter"><span>Instituciones</span><Select
          mode="multiple"
          allowClear
          placeholder="Todas las instituciones"
          value={institutionIds}
          onChange={value => { setLoading(true); setInstitutionIds(value) }}
          options={availableInstitutions.map(institution => ({ value: institution.id, label: institution.name }))}
          maxTagCount="responsive"
        /></label>
      </div>
    </Card>

    {error && <Alert type="error" showIcon message={error} />}
    {loading ? <Skeleton active paragraph={{ rows: 8 }} /> : isAdmin && !professionalId ? (
      <Card className="empty-workspace" bordered={false}><Empty description="Selecciona un profesional para consultar su evolución." /></Card>
    ) : data ? <>
      <Card className="dashboard-table-card" bordered={false}>
        <div className="section-card-heading">
          <div><Typography.Text className="eyebrow">DETALLE MENSUAL</Typography.Text><Typography.Title level={3}>Líquido por institución</Typography.Title></div>
          <Tag bordered={false}>{data.institutions.length} instituciones</Tag>
        </div>
        {data.institutions.length === 0 ? <Empty description="No hay instituciones asociadas a este profesional." /> : <Table<DashboardMonth>
          rowKey={row => `${row.year}-${row.month}`}
          columns={columns}
          dataSource={data.months}
          pagination={false}
          scroll={{ x: 'max-content' }}
          size="middle"
          rowClassName={row => row.periodExists ? '' : 'row-no-period'}
        />}
      </Card>

      <Card className="chart-card" bordered={false}>
        <div className="section-card-heading">
          <div><Typography.Text className="eyebrow">TENDENCIA EN EL TIEMPO</Typography.Text><Typography.Title level={3}>Evolución del líquido mensual</Typography.Title></div>
          <Typography.Text type="secondary">Los meses sin período aparecen como espacios en la línea.</Typography.Text>
        </div>
        <IncomeLineChart data={data} />
      </Card>
    </> : null}
  </div>
}

function monthName(month: number): string {
  return new Intl.DateTimeFormat('es-CL', { month: 'long' }).format(new Date(2026, month - 1, 1))
}
