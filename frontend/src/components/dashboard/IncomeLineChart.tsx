import type { DashboardData, DashboardInstitution } from '../../types/api'

const COLORS = ['#1c7c78', '#df8a42', '#456d9c', '#a75e6b', '#8574b2', '#4f8b62', '#c2553f', '#517f93']
const TOTAL_COLOR = '#10283f'

export function IncomeLineChart({ data }: { data: DashboardData }) {
  const width = Math.max(960, data.months.length * 24)
  const height = 300
  const inset = { left: 62, right: 24, top: 22, bottom: 54 }
  const chartWidth = width - inset.left - inset.right
  const chartHeight = height - inset.top - inset.bottom
  const allValues = data.months.flatMap(month => [
    ...(month.totalNetClp === null ? [] : [month.totalNetClp]),
    ...month.institutions.flatMap(item => item.netTotalClp === null ? [] : [item.netTotalClp]),
  ])
  const maximum = Math.max(1, ...allValues)
  const xFor = (index: number) => inset.left + (data.months.length <= 1 ? chartWidth / 2 : index * chartWidth / (data.months.length - 1))
  const yFor = (value: number) => inset.top + chartHeight - (value / maximum) * chartHeight

  const lines = [
    ...data.institutions.map((institution, index) => ({
      institution,
      color: COLORS[index % COLORS.length] ?? COLORS[0]!,
      total: false,
    })),
    { institution: null as DashboardInstitution | null, color: TOTAL_COLOR, total: true },
  ]

  const pathFor = (line: (typeof lines)[number]) => {
    let path = ''
    let open = false
    data.months.forEach((month, index) => {
      const value = line.total
        ? month.totalNetClp
        : month.institutions.find(item => item.institutionKey === line.institution?.key)?.netTotalClp ?? null
      if (value === null) {
        open = false
        return
      }
      path += `${open ? 'L' : 'M'} ${xFor(index)} ${yFor(value)} `
      open = true
    })
    return path.trim()
  }

  if (data.months.length === 0) return <div className="chart-empty">No hay meses para mostrar en este intervalo.</div>

  return <div className="chart-scroll">
    <svg className="income-chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Evolución mensual del líquido por institución">
      {[0, 0.25, 0.5, 0.75, 1].map(ratio => {
        const y = inset.top + chartHeight * ratio
        const value = Math.round(maximum * (1 - ratio))
        return <g key={ratio}>
          <line x1={inset.left} y1={y} x2={width - inset.right} y2={y} className="chart-grid-line" />
          <text x={inset.left - 10} y={y + 4} textAnchor="end" className="chart-axis-label">{compactCurrency(value)}</text>
        </g>
      })}
      {lines.map((line, index) => {
        const path = pathFor(line)
        if (!path) return null
        return <path key={line.total ? 'total' : line.institution?.key ?? index} d={path} fill="none" stroke={line.color}
          strokeWidth={line.total ? 3.5 : 2} strokeLinecap="round" strokeLinejoin="round" opacity={line.total ? 1 : 0.78} />
      })}
      {data.months.map((month, index) => index % Math.max(1, Math.ceil(data.months.length / 18)) === 0 && (
        <text key={`${month.year}-${month.month}`} x={xFor(index)} y={height - 18} textAnchor="middle" className="chart-axis-label">
          {new Intl.DateTimeFormat('es-CL', { month: 'short' }).format(new Date(month.year, month.month - 1, 1))} {String(month.year).slice(-2)}
        </text>
      ))}
    </svg>
    <div className="chart-legend">
      {lines.map((line, index) => <span key={line.total ? 'total' : line.institution?.key ?? index}>
        <i style={{ backgroundColor: line.color }} />{line.total ? 'Total mensual' : line.institution?.name}
      </span>)}
    </div>
  </div>
}

function compactCurrency(value: number): string {
  if (value >= 1_000_000) return `$${(value / 1_000_000).toFixed(value >= 10_000_000 ? 0 : 1)}M`
  if (value >= 10_000) return `$${Math.round(value / 1_000)}k`
  return `$${new Intl.NumberFormat('es-CL').format(value)}`
}
