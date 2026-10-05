import { Button, Card, Typography } from 'antd'
import { Link } from 'react-router-dom'

export function ForbiddenPage() {
  return <div className="simple-state-page"><Card bordered={false}>
    <Typography.Text className="eyebrow">403 · ACCESO DENEGADO</Typography.Text>
    <Typography.Title level={2}>Esta acción no está disponible para tu cuenta.</Typography.Title>
    <Typography.Paragraph>El backend valida cada permiso y recurso antes de permitir cambios.</Typography.Paragraph>
    <Button type="primary"><Link to="/dashboard">Volver al Dashboard</Link></Button>
  </Card></div>
}
