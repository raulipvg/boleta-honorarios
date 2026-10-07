import { Button, DatePicker, Space } from 'antd'
import type { Dayjs } from 'dayjs'
import { monthLabel } from '../../utils/date'

export function MonthSelector({
  value,
  onChange,
  disabled = false,
}: {
  value: Dayjs
  onChange: (month: Dayjs) => void
  disabled?: boolean
}) {
  const changeBy = (amount: number) => onChange(value.add(amount, 'month').date(1))

  return <Space className="month-switcher" size={8}>
    <Button aria-label="Mes anterior" disabled={disabled} onClick={() => changeBy(-1)}>‹</Button>
    <DatePicker
      picker="month"
      allowClear={false}
      value={value}
      onChange={month => { if (month) onChange(month.date(1)) }}
      format={(month: Dayjs) => monthLabel(month.year(), month.month() + 1)}
      placement="bottomLeft"
      popupAlign={{
        points: ['tc', 'bc'],
        offset: [0, 4],
        overflow: { adjustX: 1, adjustY: 1 },
      }}
      inputReadOnly
      disabled={disabled}
    />
    <Button aria-label="Mes siguiente" disabled={disabled} onClick={() => changeBy(1)}>›</Button>
  </Space>
}
