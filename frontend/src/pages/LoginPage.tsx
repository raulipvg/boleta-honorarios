import { useState } from 'react'
import { Alert, Button, Card, Form, Input, Typography, message } from 'antd'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { getApiErrorMessage } from '../services/apiClient'

interface LoginForm {
  userName: string
  password: string
}

export function LoginPage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const state = location.state as { from?: string } | null

  if (auth.status === 'loading') return <div className="page-loading"><span className="loading-dot" /></div>
  if (auth.status === 'authenticated') return <Navigate to={state?.from ?? '/dashboard'} replace />
  if (auth.status === 'password-change') return <Navigate to="/password-change" replace />

  const onFinish = async ({ userName, password }: LoginForm) => {
    setSubmitting(true)
    setError(null)
    try {
      const result = await auth.login(userName, password)
      if (result === 'password-change') navigate('/password-change', { replace: true })
      else navigate(state?.from ?? '/dashboard', { replace: true })
    } catch (requestError) {
      const status = (requestError as { response?: { status?: number } }).response?.status
      setError(status === 401
        ? 'Nombre de usuario o contraseña incorrectos.'
        : getApiErrorMessage(requestError, 'No se pudo iniciar sesión. Intenta nuevamente.'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="auth-page">
      <div className="auth-visual">
        <div className="auth-visual-content">
          <span className="brand-mark brand-mark-large">H</span>
          <Typography.Title>Tu trabajo,<br /><span>en perspectiva.</span></Typography.Title>
          <Typography.Paragraph>Registra tus horas, entiende tus ingresos y vuelve a enfocarte en lo que haces mejor.</Typography.Paragraph>
          <div className="auth-visual-orbit orbit-one" />
          <div className="auth-visual-orbit orbit-two" />
          <div className="auth-visual-orbit orbit-three" />
        </div>
        <Typography.Text className="auth-visual-foot">GESTIÓN DE INGRESOS POR HONORARIOS</Typography.Text>
      </div>
      <section className="auth-form-side">
        <Card className="auth-card" bordered={false}>
          <div className="auth-card-heading">
            <Typography.Text className="eyebrow">BIENVENIDO DE VUELTA</Typography.Text>
            <Typography.Title level={2}>Inicia sesión</Typography.Title>
            <Typography.Paragraph>Ingresa con el nombre de usuario asignado por tu administrador.</Typography.Paragraph>
          </div>
          {error && <Alert type="error" showIcon message={error} className="form-alert" />}
          <Form<LoginForm> layout="vertical" requiredMark={false} onFinish={onFinish} size="large">
            <Form.Item name="userName" label="Nombre de usuario" rules={[{ required: true, message: 'Ingresa tu nombre de usuario.' }]}>
              <Input autoComplete="username" placeholder="Tu nombre de usuario" autoFocus />
            </Form.Item>
            <Form.Item name="password" label="Contraseña" rules={[{ required: true, message: 'Ingresa tu contraseña.' }]}>
              <Input.Password autoComplete="current-password" placeholder="Tu contraseña" />
            </Form.Item>
            <Button type="primary" htmlType="submit" block loading={submitting} className="primary-action">
              Entrar a mi espacio <span aria-hidden>→</span>
            </Button>
          </Form>
          <Typography.Text className="auth-help">¿Necesitas una contraseña nueva? Comunícate con el administrador.</Typography.Text>
        </Card>
        <Typography.Text className="auth-copyright">© {new Date().getFullYear()} Gestión de honorarios</Typography.Text>
      </section>
    </main>
  )
}

export function PasswordChangePage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  if (auth.status === 'loading') return <div className="page-loading"><span className="loading-dot" /></div>
  if (auth.status === 'anonymous') return <Navigate to="/login" replace />
  if (auth.status === 'authenticated') return <Navigate to="/dashboard" replace />

  const onFinish = async (values: { currentPassword: string; newPassword: string; confirmPassword: string }) => {
    if (values.newPassword !== values.confirmPassword) {
      setError('Las contraseñas nuevas no coinciden.')
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      await auth.changePassword(values.currentPassword, values.newPassword)
      setDone(true)
      message.success('Contraseña actualizada. Inicia sesión con la nueva contraseña.')
      navigate('/login', { replace: true })
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'No se pudo cambiar la contraseña.'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="password-change-page">
      <Card className="password-change-card" bordered={false}>
        <span className="brand-mark">H</span>
        <Typography.Text className="eyebrow">{auth.temporaryUserName ?? 'SEGURIDAD DE LA CUENTA'}</Typography.Text>
        <Typography.Title level={2}>Crea tu contraseña personal</Typography.Title>
        <Typography.Paragraph>La contraseña temporal debe cambiarse antes de continuar. Usa al menos 15 caracteres.</Typography.Paragraph>
        {error && <Alert type="error" showIcon message={error} className="form-alert" />}
        {!done && <Form layout="vertical" onFinish={onFinish} requiredMark={false}>
          <Form.Item name="currentPassword" label="Contraseña temporal o actual" rules={[{ required: true }]}>
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Form.Item name="newPassword" label="Nueva contraseña" rules={[{ required: true, min: 15, message: 'Usa al menos 15 caracteres.' }]}>
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Form.Item name="confirmPassword" label="Repite la nueva contraseña" rules={[{ required: true }]}>
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={submitting} className="primary-action">Actualizar contraseña</Button>
        </Form>}
      </Card>
    </main>
  )
}
