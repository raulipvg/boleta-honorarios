import dayjs, { type Dayjs } from 'dayjs'
import 'dayjs/locale/es'

dayjs.locale('es')

export function monthLabel(year: number, month: number): string {
  const label = dayjs(`${year}-${String(month).padStart(2, '0')}-01`).format('MMMM YYYY')
  return `${label.charAt(0).toLocaleUpperCase('es-CL')}${label.slice(1)}`
}

export function monthValue(value: Dayjs): { year: number; month: number } {
  return { year: value.year(), month: value.month() + 1 }
}
