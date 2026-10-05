import { ConfigProvider, Spin } from 'antd'
import esES from 'antd/locale/es_ES'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { lazy, Suspense } from 'react'
import { AuthProvider } from './context/AuthProvider'
import { useAuth } from './hooks/useAuth'
import { ProtectedRoute } from './components/ProtectedRoute'
import { AppShell } from './components/layout/AppShell'
import { PermissionCodes } from './constants/authorization'
import './styles.css'

const DashboardPage = lazy(() => import('./pages/DashboardPage').then(page => ({ default: page.DashboardPage })))
const MonthWorkspacePage = lazy(() => import('./pages/MonthWorkspacePage').then(page => ({ default: page.MonthWorkspacePage })))
const InstitutionsRatesPage = lazy(() => import('./pages/InstitutionsRatesPage').then(page => ({ default: page.InstitutionsRatesPage })))
const AdminUsersPage = lazy(() => import('./pages/AdminUsersPage').then(page => ({ default: page.AdminUsersPage })))
const AdminInstitutionsPage = lazy(() => import('./pages/AdminInstitutionsPage').then(page => ({ default: page.AdminInstitutionsPage })))
const SettingsPage = lazy(() => import('./pages/SettingsPage').then(page => ({ default: page.SettingsPage })))
const ForbiddenPage = lazy(() => import('./pages/ForbiddenPage').then(page => ({ default: page.ForbiddenPage })))
const LoginPage = lazy(() => import('./pages/LoginPage').then(page => ({ default: page.LoginPage })))
const PasswordChangePage = lazy(() => import('./pages/LoginPage').then(page => ({ default: page.PasswordChangePage })))

export default function App() {
  return <ConfigProvider locale={esES} theme={{
    token: {
      colorPrimary: '#167d78',
      colorInfo: '#167d78',
      colorSuccess: '#23805e',
      colorWarning: '#d08a3c',
      colorError: '#c7574d',
      borderRadius: 10,
      fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
      controlHeight: 42,
    },
    components: {
      Card: { borderRadiusLG: 18 },
      Table: { headerBg: '#f6f8f8', rowHoverBg: '#f4faf9' },
    },
  }}>
    <BrowserRouter>
      <AuthProvider><ApplicationRoutes /></AuthProvider>
    </BrowserRouter>
  </ConfigProvider>
}

function ApplicationRoutes() {
  return <Suspense fallback={<div className="page-loading"><Spin size="large" /></div>}><Routes>
    <Route path="/login" element={<LoginPage />} />
    <Route path="/password-change" element={<PasswordChangeRoute />} />
    <Route path="/forbidden" element={<ForbiddenPage />} />
    <Route element={<ProtectedRoute />}>
      <Route element={<AppShell />}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route element={<ProtectedRoute requiredPermissions={[PermissionCodes.dashboardRead]} />}>
          <Route path="/dashboard" element={<DashboardPage />} />
        </Route>
        <Route element={<ProtectedRoute requiredPermissions={[PermissionCodes.periodsRead]} />}>
          <Route path="/month" element={<MonthWorkspacePage />} />
        </Route>
        <Route element={<ProtectedRoute requiredPermissions={[PermissionCodes.relationshipsRead]} />}>
          <Route path="/institutions" element={<InstitutionsRatesPage />} />
        </Route>
        <Route element={<ProtectedRoute requiredPermissions={[PermissionCodes.retentionRead]} />}>
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
        <Route element={<ProtectedRoute requiredPermissions={[PermissionCodes.usersRead]} />}>
          <Route path="/admin/users" element={<AdminUsersPage />} />
        </Route>
        <Route element={<ProtectedRoute requiredPermissions={[PermissionCodes.institutionsManage]} />}>
          <Route path="/admin/institutions" element={<AdminInstitutionsPage />} />
        </Route>
      </Route>
    </Route>
    <Route path="*" element={<Navigate to="/dashboard" replace />} />
  </Routes></Suspense>
}

function PasswordChangeRoute() {
  const { status } = useAuth()
  if (status === 'loading') return <div className="page-loading"><Spin size="large" /></div>
  if (status === 'anonymous') return <Navigate to="/login" replace />
  if (status === 'authenticated') return <Navigate to="/dashboard" replace />
  return <PasswordChangePage />
}
