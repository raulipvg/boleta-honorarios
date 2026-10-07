import type { DashboardInstitution } from '../../types/api'

export const TOTAL_MONTHLY_COLOR = '#10283f'

const FIXED_INSTITUTION_COLORS: Record<string, string> = {
  centrocebien: '#1c7c78',
  sapulorenzoarenas: '#456d9c',
  sartucapel: '#a75e6b',
  sanatorioaleman: '#df8a42',
}

export function institutionSeriesColor(institution: DashboardInstitution): string {
  const normalizedName = normalizeName(institution.name)
  const fixedColor = FIXED_INSTITUTION_COLORS[normalizedName]
  return fixedColor ?? stableColorFromKey(institution.key)
}

export function dashboardColorTint(color: string, strength: number): string {
  return `color-mix(in srgb, ${color} ${strength}%, white)`
}

function normalizeName(value: string): string {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('es-CL').replace(/[^a-z0-9]/g, '')
}

function stableColorFromKey(key: string): string {
  let hash = 0x811c9dc5
  for (let index = 0; index < key.length; index++)
    hash = Math.imul(hash ^ key.charCodeAt(index), 0x01000193)

  const hue = (hash >>> 0) % 36_000 / 100
  return `hsl(${hue} 58% 38%)`
}
