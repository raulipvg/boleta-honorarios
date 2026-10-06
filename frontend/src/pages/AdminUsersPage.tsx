import { useEffect, useState } from 'react'
import { Alert, App as AntdApp, Button, Card, Form, Input, Modal, Select, Space, Table, Tag, Typography, type TableColumnsType } from 'antd'
import { adminUserService } from '../services/administration/adminUserService'
import { getApiErrorMessage } from '../services/apiClient'
import type { AccountSummary } from '../types/api'
import { RoleCodes } from '../constants/authorization'

interface NewAccountForm {
  userName: string
  temporaryPassword: string
  roleCodes: string[]
  professionalName?: string
}

export function AdminUsersPage() {
  const { message } = AntdApp.useApp()
  const [accounts, setAccounts] = useState<AccountSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [resetAccount, setResetAccount] = useState<AccountSummary | null>(null)
  const [roles, setRoles] = useState<string[]>([])
  const [createForm] = Form.useForm<NewAccountForm>()
  const [resetForm] = Form.useForm<{ temporaryPassword: string }>()

  const reload = async () => setAccounts(await adminUserService.list())
  useEffect(() => {
    let active = true
    adminUserService.list()
      .then(result => { if (active) setAccounts(result) })
      .catch(requestError => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  const createAccount = async (values: NewAccountForm) => {
    setSubmitting(true)
    setError(null)
    try {
      await adminUserService.create(values)
      await reload()
      createForm.resetFields()
      setRoles([])
      message.success('Cuenta creada. La persona deberá cambiar la contraseña temporal al ingresar.')
    } catch (requestError) { setError(getApiErrorMessage(requestError)) }
    finally { setSubmitting(false) }
  }

  const resetPassword = async (values: { temporaryPassword: string }) => {
    if (!resetAccount) return
    setSubmitting(true)
    setError(null)
    try {
      await adminUserService.resetPassword(resetAccount.id, values.temporaryPassword)
      await reload()
      setResetAccount(null)
      resetForm.resetFields()
      message.success('Contraseña restablecida. Se exigirá cambiarla en el próximo acceso.')
    } catch (requestError) { setError(getApiErrorMessage(requestError)) }
    finally { setSubmitting(false) }
  }

  const columns: TableColumnsType<AccountSummary> = [
    { title: 'Nombre de usuario', dataIndex: 'userName', key: 'userName', render: value => <Typography.Text strong>{value}</Typography.Text> },
    { title: 'Perfil', dataIndex: 'professionalName', key: 'professionalName', render: value => value ?? <Typography.Text type="secondary">—</Typography.Text> },
    { title: 'Roles', dataIndex: 'roleCodes', key: 'roles', render: values => <Space wrap>{(values as string[]).map(role => <Tag key={role} color={role === RoleCodes.administrator ? 'geekblue' : 'cyan'}>{role === RoleCodes.administrator ? 'Administrador' : 'Profesional'}</Tag>)}</Space> },
    { title: 'Estado', key: 'state', render: (_, account) => account.active ? <Tag color="green">Activa</Tag> : <Tag color="red">Inactiva</Tag> },
    { title: 'Acceso', key: 'password', render: (_, account) => account.mustChangePassword ? <Tag color="gold">Debe cambiar contraseña</Tag> : <Tag>Configurado</Tag> },
    { title: '', key: 'actions', align: 'right', render: (_, account) => <Button size="small" onClick={() => setResetAccount(account)}>Restablecer contraseña</Button> },
  ]

  return <div className="page-stack">
    <section className="page-heading"><div><Typography.Text className="eyebrow">ADMINISTRACIÓN</Typography.Text><Typography.Title level={1}>Acceso de las personas.</Typography.Title>
      <Typography.Paragraph>Crea cuentas profesionales y administra sus credenciales temporales.</Typography.Paragraph></div>
      <Tag color="geekblue">Sin registro público</Tag></section>
    {error && <Alert type="error" showIcon title={error} closable={{ onClose: () => setError(null) }} />}

    <Card className="create-account-card" variant="borderless">
      <div className="section-card-heading"><div><Typography.Text className="eyebrow">NUEVA CUENTA</Typography.Text><Typography.Title level={3}>Crear usuario</Typography.Title></div>
        <Typography.Text type="secondary">Las contraseñas temporales no se registran ni se vuelven a mostrar.</Typography.Text></div>
      <Form form={createForm} layout="vertical" onFinish={createAccount} requiredMark={false} className="account-create-grid">
        <Form.Item name="userName" label="Nombre de usuario" rules={[{ required: true }, { max: 256 }]}><Input autoComplete="off" placeholder="nombre.usuario" /></Form.Item>
        <Form.Item name="temporaryPassword" label="Contraseña temporal" rules={[{ required: true }, { min: 15, message: 'Usa al menos 15 caracteres.' }]}>
          <Input.Password autoComplete="new-password" placeholder="Mínimo 15 caracteres" />
        </Form.Item>
        <Form.Item name="roleCodes" label="Roles" rules={[{ required: true, message: 'Asigna al menos un rol.' }]}>
          <Select mode="multiple" value={roles} onChange={setRoles} options={[
            { value: RoleCodes.professional, label: 'Profesional' },
            { value: RoleCodes.administrator, label: 'Administrador' },
          ]} placeholder="Selecciona roles" />
        </Form.Item>
        {roles.includes(RoleCodes.professional) && <Form.Item name="professionalName" label="Nombre del perfil profesional" rules={[{ required: true }, { max: 200 }]}> 
          <Input placeholder="Nombre completo" />
        </Form.Item>}
        <div className="account-create-submit"><Button type="primary" htmlType="submit" loading={submitting}>Crear cuenta</Button></div>
      </Form>
      <Alert type="info" showIcon title="Entrega la contraseña temporal por un canal seguro. La persona deberá definir su propia contraseña en el primer ingreso." />
    </Card>

    <Card className="dashboard-table-card" variant="borderless">
      <div className="section-card-heading"><div><Typography.Text className="eyebrow">CUENTAS DEL SISTEMA</Typography.Text><Typography.Title level={3}>Usuarios registrados</Typography.Title></div>
        <Tag variant="filled">{accounts.length} cuentas</Tag></div>
      <Table<AccountSummary> rowKey="id" columns={columns} dataSource={accounts} loading={loading} pagination={{ pageSize: 10, showSizeChanger: false }} scroll={{ x: 900 }} />
    </Card>

    <Modal title={`Restablecer contraseña · ${resetAccount?.userName ?? ''}`} open={Boolean(resetAccount)} onCancel={() => setResetAccount(null)} footer={null} destroyOnHidden>
      <Typography.Paragraph>Asigna una contraseña temporal. Se cerrarán sus sesiones y se solicitará un cambio obligatorio al volver a ingresar.</Typography.Paragraph>
      <Form form={resetForm} layout="vertical" onFinish={resetPassword} requiredMark={false}>
        <Form.Item name="temporaryPassword" label="Nueva contraseña temporal" rules={[{ required: true }, { min: 15, message: 'Usa al menos 15 caracteres.' }]}>
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={submitting}>Restablecer contraseña</Button>
      </Form>
    </Modal>
  </div>
}
