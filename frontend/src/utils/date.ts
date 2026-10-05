import dayjs, { type Dayjs } from 'dayjs'
import 'dayjs/locale/es'

dayjs.locale('es')

export function monthLabel(year: number, month: number): string {
  return dayjs(`${year}-${String(month).padStart(2, '0')}-01`).format('MMMM YYYY')
}

export function monthValue(value: Dayjs): { year: number; month: number } {
  return { year: value.year(), month: value.month() + 1 }
}
