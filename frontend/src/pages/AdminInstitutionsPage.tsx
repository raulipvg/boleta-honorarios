import { useEffect, useState } from 'react'
import { Alert, App as AntdApp, Button, Card, Form, Input, Switch, Table, Tag, Typography, type TableColumnsType } from 'antd'
import { incomeService } from '../services/incomeService'
import { getApiErrorMessage } from '../services/apiClient'
import type { Institution } from '../types/api'

export function AdminInstitutionsPage() {
  const { message } = AntdApp.useApp()
  const [items, setItems] = useState<Institution[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [form] = Form.useForm<{ name: string }>()

  const reload = async () => setItems(await incomeService.institutions())
  useEffect(() => {
    let active = true
    incomeService.institutions()
      .then(result => { if (active) setItems(result) })
      .catch(requestError => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  const addInstitution = async ({ name }: { name: string }) => {
    setSubmitting(true)
    setError(null)
    try {
      await incomeService.createInstitution(name)
      form.resetFields()
      await reload()
      message.success('Institución agregada al catálogo público.')
    } catch (requestError) { setError(getApiErrorMessage(requestError)) }
    finally { setSubmitting(false) }
  }

  const toggle = async (item: Institution, active: boolean) => {
    try {
      await incomeService.updateInstitution(item.id, item.name, active)
      await reload()
    } catch (requestError) { setError(getApiErrorMessage(requestError)) }
  }

  const columns: TableColumnsType<Institution> = [
    { title: 'Nombre', dataIndex: 'name', key: 'name', render: value => <Typography.Text strong>{value}</Typography.Text> },
    { title: 'Estado', key: 'state', render: (_, item) => item.active ? <Tag color="green">Activa</Tag> : <Tag>Inactiva</Tag> },
    { title: 'Disponible para profesionales', key: 'active', align: 'right', render: (_, item) => <Switch checked={item.active} onChange={value => void toggle(item, value)} aria-label={`Cambiar estado de ${item.name}`} /> },
  ]

  return <div className="page-stack">
    <section className="page-heading"><div><Typography.Text className="eyebrow">CATÁLOGO COMPARTIDO</Typography.Text><Typography.Title level={1}>Instituciones públicas.</Typography.Title>
      <Typography.Paragraph>Administra las opciones que los profesionales podrán asociar a su actividad.</Typography.Paragraph></div><Tag color="geekblue">Solo Administrador</Tag></section>
    {error && <Alert type="error" showIcon title={error} />}
    <Card className="create-account-card" variant="borderless">
      <div className="section-card-heading"><div><Typography.Text className="eyebrow">CATÁLOGO PÚBLICO</Typography.Text><Typography.Title level={3}>Agregar institución</Typography.Title></div></div>
      <Form form={form} layout="inline" onFinish={addInstitution} requiredMark={false}>
        <Form.Item name="name" rules={[{ required: true, whitespace: true, message: 'Ingresa un nombre.' }, { max: 200 }]}>
          <Input placeholder="Nombre de la institución" maxLength={200} style={{ minWidth: 320 }} />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={submitting}>Agregar al catálogo</Button>
      </Form>
    </Card>
    <Card className="dashboard-table-card" variant="borderless">
      <div className="section-card-heading"><div><Typography.Text className="eyebrow">INSTITUCIONES</Typography.Text><Typography.Title level={3}>Catálogo actual</Typography.Title></div><Tag variant="filled">{items.length} instituciones</Tag></div>
      <Table<Institution> rowKey="id" columns={columns} dataSource={items} loading={loading} pagination={{ pageSize: 12, showSizeChanger: false }} />
    </Card>
  </div>
}
