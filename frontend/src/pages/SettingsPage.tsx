import { useEffect, useState } from 'react'
import { Alert, App as AntdApp, Button, Card, Col, Form, Input, Row, Skeleton, Table, Tag, Typography, type TableColumnsType } from 'antd'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { profileService } from '../services/profile/profileService'
import { retentionService } from '../services/configuration/retentionService'
import { getApiErrorMessage } from '../services/apiClient'
import { formatRate } from '../utils/format'
import type { RetentionRate } from '../types/api'
import { PermissionCodes } from '../constants/authorization'

interface ProfileForm { name: string }
interface PasswordForm { currentPassword: string; newPassword: string; confirmPassword: string }

export function SettingsPage() {
  const { message } = AntdApp.useApp()
  const auth = useAuth()
  const navigate = useNavigate()
  const canReadProfile = auth.hasPermission(PermissionCodes.profileRead)
  const canEditProfile = auth.hasPermission(PermissionCodes.profileUpdate)
  const [profileName, setProfileName] = useState('')
  const [retentionRates, setRetentionRates] = useState<RetentionRate[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [form] = Form.useForm<ProfileForm>()
  const [passwordForm] = Form.useForm<PasswordForm>()

  useEffect(() => {
    const jobs: Promise<unknown>[] = [retentionService.list().then(setRetentionRates)]
    if (canReadProfile) jobs.push(profileService.get().then(profile => { setProfileName(profile.name); form.setFieldsValue({ name: profile.name }) }))
    Promise.all(jobs).catch(requestError => setError(getApiErrorMessage(requestError))).finally(() => setLoading(false))
  }, [canReadProfile, form])

  const updateProfile = async ({ name }: ProfileForm) => {
    try {
      await profileService.update(name)
      setProfileName(name)
      message.success('Perfil actualizado.')
    } catch (requestError) { setError(getApiErrorMessage(requestError)) }
  }

  const changePassword = async (values: PasswordForm) => {
    if (values.newPassword !== values.confirmPassword) {
      setError('Las contraseñas nuevas no coinciden.')
      return
    }
    try {
      await auth.changePassword(values.currentPassword, values.newPassword)
      message.success('Contraseña actualizada. Inicia sesión nuevamente.')
      navigate('/login', { replace: true })
    } catch (requestError) { setError(getApiErrorMessage(requestError)) }
  }

  const columns: TableColumnsType<RetentionRate> = [
    { title: 'Año tributario', dataIndex: 'year', key: 'year', render: value => <Typography.Text strong>{value}</Typography.Text> },
    { title: 'Retención de honorarios', dataIndex: 'percentage', key: 'percentage', render: value => formatRate(value) },
    { title: 'Origen', key: 'source', render: () => <Tag color="blue">Configuración controlada</Tag> },
  ]

  return <div className="page-stack">
    <section className="page-heading"><div><Typography.Text className="eyebrow">PREFERENCIAS Y SEGURIDAD</Typography.Text><Typography.Title level={1}>Configuración.</Typography.Title>
      <Typography.Paragraph>{canReadProfile ? `Perfil de ${profileName || auth.identity?.userName}.` : 'Parámetros globales de lectura y seguridad de la cuenta.'}</Typography.Paragraph></div></section>
    {error && <Alert type="error" showIcon title={error} closable={{ onClose: () => setError(null) }} />}
    {loading ? <Skeleton active /> : <>
      <Row gutter={[18, 18]}>
        {canReadProfile && <Col xs={24} lg={8}>
          <Card className="settings-card" variant="borderless">
            <div className="section-card-heading"><div><Typography.Text className="eyebrow">PERFIL PROFESIONAL</Typography.Text><Typography.Title level={3}>Tus datos</Typography.Title></div></div>
            <Form form={form} layout="vertical" onFinish={updateProfile} requiredMark={false}>
              <Form.Item name="name" label="Nombre del perfil" rules={[{ required: true, whitespace: true }, { max: 200 }]}><Input disabled={!canEditProfile} maxLength={200} /></Form.Item>
              {canEditProfile && <Row justify="end"><Button type="primary" htmlType="submit">Guardar perfil</Button></Row>}
            </Form>
          </Card>
        </Col>}

        <Col xs={24} lg={canReadProfile ? 16 : 24}>
          <Card className="settings-card" variant="borderless">
            <div className="section-card-heading"><div><Typography.Text className="eyebrow">SEGURIDAD</Typography.Text><Typography.Title level={3}>Cambiar contraseña</Typography.Title></div></div>
            <Typography.Paragraph>Usa al menos 15 caracteres. Al actualizarla, se cerrarán las sesiones activas.</Typography.Paragraph>
            <Form form={passwordForm} layout="vertical" onFinish={changePassword} requiredMark={false}>
              <Row gutter={[12, 0]}>
                <Col xs={24} lg={8}>
                  <Form.Item name="currentPassword" label="Contraseña actual" rules={[{ required: true }]}><Input.Password autoComplete="current-password" /></Form.Item>
                </Col>
                <Col xs={24} lg={8}>
                  <Form.Item name="newPassword" label="Nueva contraseña" rules={[{ required: true }, { min: 15, message: 'Usa al menos 15 caracteres.' }]}><Input.Password autoComplete="new-password" /></Form.Item>
                </Col>
                <Col xs={24} lg={8}>
                  <Form.Item name="confirmPassword" label="Repetir nueva contraseña" rules={[{ required: true }]}><Input.Password autoComplete="new-password" /></Form.Item>
                </Col>
              </Row>
              <Row justify="end"><Button type="primary" htmlType="submit">Actualizar contraseña</Button></Row>
            </Form>
          </Card>
        </Col>
      </Row>

      <Card className="settings-card" variant="borderless">
        <div className="section-card-heading"><div><Typography.Text className="eyebrow">PARÁMETROS ANUALES</Typography.Text><Typography.Title level={3}>Retención de boletas</Typography.Title></div><Tag>Solo lectura</Tag></div>
        <Typography.Paragraph>Las tasas se administran mediante configuración controlada. Cada período conserva la tasa que tenía al crearse.</Typography.Paragraph>
        <Table<RetentionRate> rowKey="year" columns={columns} dataSource={retentionRates} pagination={false} />
      </Card>
    </>}
  </div>
}
