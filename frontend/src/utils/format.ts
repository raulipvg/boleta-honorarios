export const clp = new Intl.NumberFormat('es-CL', {
  style: 'currency',
  currency: 'CLP',
  maximumFractionDigits: 0,
})

export function formatClp(value: number): string {
  return clp.format(value)
}

export function formatParticipation(value: number | null, total: number | null): string {
  if (value === null || total === null || total === 0) return '—'
  return `${Math.round(value / total * 100)} %`
}

export function formatPercent(value: number): string {
  return `${new Intl.NumberFormat('es-CL', { maximumFractionDigits: 0 }).format(Math.round(value))} %`
}

export function formatRate(value: number): string {
  return `${new Intl.NumberFormat('es-CL', { maximumFractionDigits: 2 }).format(value)} %`
}
