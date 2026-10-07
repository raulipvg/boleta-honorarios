import { useMemo } from 'react'
import { Avatar, Button, Layout, Menu, Space, Tag, Typography } from 'antd'
import { Link, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { PermissionCodes, RoleCodes } from '../../constants/authorization'

const { Header, Content, Sider } = Layout

export function AppShell() {
  const auth = useAuth()
  const location = useLocation()
  const items = useMemo(() => {
    const entries = [
      { key: '/dashboard', label: 'Dashboard', permission: PermissionCodes.dashboardRead },
      { key: '/month', label: 'Trabajo por Horas', permission: PermissionCodes.periodsRead },
      { key: '/private-liquidations', label: 'Trabajo por Paciente', permission: PermissionCodes.privateLiquidationsRead },
      { key: '/institutions', label: 'Instituciones y Tarifas por Hora', permission: PermissionCodes.relationshipsRead },
      { key: '/settings', label: 'Configuración', permission: PermissionCodes.retentionRead },
      { key: '/admin/users', label: 'Cuentas', permission: PermissionCodes.usersRead },
      { key: '/admin/institutions', label: 'Catálogo público', permission: PermissionCodes.institutionsManage },
    ]
    return entries.filter(entry => auth.hasPermission(entry.permission)).map(entry => ({
      key: entry.key,
      label: <Link to={entry.key}>{entry.label}</Link>,
    }))
  }, [auth])

  const roleLabel = auth.identity?.roleCodes.map(role => role === RoleCodes.administrator ? 'Administrador' : 'Profesional').join(' · ')
  const initial = auth.identity?.userName.slice(0, 1).toUpperCase() ?? 'U'

  return (
    <Layout className="app-layout">
      <Sider breakpoint="lg" collapsedWidth="0" className="app-sider">
        <Link to="/dashboard" className="brand-lockup">
          <span className="brand-mark">H</span>
          <span><strong>Honorarios</strong><small>Gestión mensual</small></span>
        </Link>
        <Menu theme="dark" mode="inline" selectedKeys={[location.pathname]} items={items} />
        <div className="sider-note">Control claro, mes a mes.</div>
      </Sider>
      <Layout>
        <Header className="topbar">
          <div className="topbar-context">
            <Typography.Text className="eyebrow">GESTIÓN DE INGRESOS</Typography.Text>
            <Typography.Text className="topbar-subtitle">Tu actividad profesional, organizada.</Typography.Text>
          </div>
          <Space size={12}>
            <Tag color="blue">{roleLabel}</Tag>
            <Avatar className="user-avatar">{initial}</Avatar>
            <Typography.Text strong className="username">{auth.identity?.userName}</Typography.Text>
            <Button type="text" onClick={() => void auth.logout()}>Cerrar sesión</Button>
          </Space>
        </Header>
        <Content className="app-content"><Outlet /></Content>
      </Layout>
      <Button className="mobile-signout" onClick={() => void auth.logout()}>Salir</Button>
    </Layout>
  )
}
