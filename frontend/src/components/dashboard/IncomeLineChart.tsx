import { useState, type PointerEvent } from 'react'
import type { DashboardData, DashboardInstitution, DashboardMonth } from '../../types/api'
import { formatClp } from '../../utils/format'
import { monthLabel } from '../../utils/date'
import { institutionSeriesColor, TOTAL_MONTHLY_COLOR } from './seriesColors'

interface ChartSeries {
  key: string
  label: string
  institution: DashboardInstitution | null
  color: string
  total: boolean
}

interface HoveredPoint {
  seriesKey: string
  monthIndex: number
}

export function IncomeLineChart({ data }: { data: DashboardData }) {
  const [hoveredPoint, setHoveredPoint] = useState<HoveredPoint | null>(null)
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

  const lines: ChartSeries[] = [
    ...data.institutions.map(institution => ({
      key: institution.key,
      label: institution.name,
      institution,
      color: institutionSeriesColor(institution),
      total: false,
    })),
    { key: 'total-monthly', label: 'Total Mensual', institution: null, color: TOTAL_MONTHLY_COLOR, total: true },
  ]

  const valueFor = (line: ChartSeries, month: DashboardMonth): number | null => line.total
    ? month.totalNetClp
    : month.institutions.find(item => item.institutionKey === line.institution?.key)?.netTotalClp ?? null

  const pathFor = (line: ChartSeries) => {
    let path = ''
    let open = false
    data.months.forEach((month, index) => {
      const value = valueFor(line, month)
      if (value === null) {
        open = false
        return
      }
      path += `${open ? 'L' : 'M'} ${xFor(index)} ${yFor(value)} `
      open = true
    })
    return path.trim()
  }

  const nearestMonthIndex = (event: PointerEvent<SVGPathElement>, line: ChartSeries): number | null => {
    const svg = event.currentTarget.ownerSVGElement
    const matrix = svg?.getScreenCTM()
    if (!svg || !matrix) return null

    const pointer = svg.createSVGPoint()
    pointer.x = event.clientX
    pointer.y = event.clientY
    const chartPointer = pointer.matrixTransform(matrix.inverse())
    let nearestIndex: number | null = null
    let nearestDistance = Number.POSITIVE_INFINITY

    data.months.forEach((month, index) => {
      if (valueFor(line, month) === null) return
      const distance = Math.abs(xFor(index) - chartPointer.x)
      if (distance < nearestDistance) {
        nearestDistance = distance
        nearestIndex = index
      }
    })

    const monthSpacing = data.months.length <= 1 ? chartWidth : chartWidth / (data.months.length - 1)
    return nearestDistance <= Math.max(8, monthSpacing * 0.65) ? nearestIndex : null
  }

  const hoveredSeries = hoveredPoint === null ? undefined : lines.find(line => line.key === hoveredPoint.seriesKey)
  const hoveredMonth = hoveredPoint === null ? undefined : data.months[hoveredPoint.monthIndex]
  const hoveredValue = hoveredSeries && hoveredMonth ? valueFor(hoveredSeries, hoveredMonth) : null
  const tooltip = hoveredSeries && hoveredMonth && hoveredValue !== null && hoveredPoint !== null
    ? {
      x: xFor(hoveredPoint.monthIndex),
      y: yFor(hoveredValue),
      label: hoveredSeries.label,
      color: hoveredSeries.color,
      month: monthLabel(hoveredMonth.year, hoveredMonth.month),
      value: formatClp(hoveredValue),
    }
    : null

  if (data.months.length === 0) return <div className="chart-empty">No hay meses para mostrar en este intervalo.</div>

  return <div className="chart-scroll">
    <svg className="income-chart" viewBox={`0 0 ${width} ${height}`} role="group" aria-label="Evolución mensual del líquido por institución">
      {[0, 0.25, 0.5, 0.75, 1].map(ratio => {
        const y = inset.top + chartHeight * ratio
        const value = Math.round(maximum * (1 - ratio))
        return <g key={ratio}>
          <line x1={inset.left} y1={y} x2={width - inset.right} y2={y} className="chart-grid-line" />
          <text x={inset.left - 10} y={y + 4} textAnchor="end" className="chart-axis-label">{compactCurrency(value)}</text>
        </g>
      })}
      {data.months.map((month, index) => index % Math.max(1, Math.ceil(data.months.length / 18)) === 0 && (
        <text key={`${month.year}-${month.month}`} x={xFor(index)} y={height - 18} textAnchor="middle" className="chart-axis-label">
          {new Intl.DateTimeFormat('es-CL', { month: 'short' }).format(new Date(month.year, month.month - 1, 1))} {String(month.year).slice(-2)}
        </text>
      ))}
      {lines.map(line => {
        const path = pathFor(line)
        if (!path) return null
        return <path
          key={`hit-${line.key}`}
          d={path}
          fill="none"
          stroke="transparent"
          strokeWidth={14}
          strokeLinecap="round"
          strokeLinejoin="round"
          pointerEvents="stroke"
          className="chart-line-hit-area"
          onPointerMove={event => {
            const monthIndex = nearestMonthIndex(event, line)
            setHoveredPoint(monthIndex === null ? null : { seriesKey: line.key, monthIndex })
          }}
          onPointerLeave={() => setHoveredPoint(current => current?.seriesKey === line.key ? null : current)}
        />
      })}
      {lines.map(line => {
        const path = pathFor(line)
        if (!path) return null
        return <path
          key={line.key}
          d={path}
          fill="none"
          stroke={line.color}
          strokeWidth={line.total ? 3.5 : 2}
          strokeLinecap="round"
          strokeLinejoin="round"
          opacity={line.total ? 1 : 0.78}
          pointerEvents="none"
        />
      })}
      {lines.flatMap(line => data.months.map((month, monthIndex) => {
        const value = valueFor(line, month)
        if (value === null) return null
        const active = hoveredPoint?.seriesKey === line.key && hoveredPoint.monthIndex === monthIndex
        const pointLabel = `${line.label}, ${monthLabel(month.year, month.month)}, ${formatClp(value)}`
        return <g
          key={`${line.key}-${month.year}-${month.month}`}
          className="chart-data-point"
          tabIndex={0}
          role="img"
          aria-label={pointLabel}
          onPointerEnter={() => setHoveredPoint({ seriesKey: line.key, monthIndex })}
          onPointerLeave={() => setHoveredPoint(current => current?.seriesKey === line.key && current.monthIndex === monthIndex ? null : current)}
          onFocus={() => setHoveredPoint({ seriesKey: line.key, monthIndex })}
          onBlur={() => setHoveredPoint(current => current?.seriesKey === line.key && current.monthIndex === monthIndex ? null : current)}
        >
          <title>{pointLabel}</title>
          <circle cx={xFor(monthIndex)} cy={yFor(value)} r={9} fill="transparent" pointerEvents="all" />
          <circle
            cx={xFor(monthIndex)}
            cy={yFor(value)}
            r={active ? 4.5 : 3}
            fill="white"
            stroke={line.color}
            strokeWidth={active ? 2.5 : 1.75}
            pointerEvents="none"
          />
        </g>
      }))}
      {tooltip && (() => {
        const tooltipWidth = 210
        const tooltipHeight = 60
        const tooltipX = Math.max(inset.left, Math.min(width - inset.right - tooltipWidth, tooltip.x - tooltipWidth / 2))
        const tooltipY = tooltip.y - tooltipHeight - 12 >= inset.top
          ? tooltip.y - tooltipHeight - 12
          : tooltip.y + 12
        return <g className="chart-tooltip" pointerEvents="none" aria-hidden="true">
          <rect x={tooltipX} y={tooltipY} width={tooltipWidth} height={tooltipHeight} rx={7} style={{ stroke: tooltip.color }} />
          <text x={tooltipX + 10} y={tooltipY + 16} className="chart-tooltip-month">{tooltip.month}</text>
          <text x={tooltipX + 10} y={tooltipY + 34} className="chart-tooltip-series">{tooltip.label}</text>
          <text x={tooltipX + 10} y={tooltipY + 51} className="chart-tooltip-value">{tooltip.value}</text>
        </g>
      })()}
    </svg>
    <div className="chart-legend">
      {lines.map((line, index) => <span key={line.total ? 'total' : line.institution?.key ?? index}>
        <i style={{ backgroundColor: line.color }} />{line.label}
      </span>)}
    </div>
  </div>
}

function compactCurrency(value: number): string {
  if (value >= 1_000_000) return `$${(value / 1_000_000).toFixed(value >= 10_000_000 ? 0 : 1)}M`
  if (value >= 10_000) return `$${Math.round(value / 1_000)}k`
  return `$${new Intl.NumberFormat('es-CL').format(value)}`
}
