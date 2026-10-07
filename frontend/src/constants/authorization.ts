export const RoleCodes = {
  administrator: 'ADMINISTRADOR',
  professional: 'PROFESIONAL',
} as const

export const PermissionCodes = {
  usersRead: 'users:account:read',
  institutionsRead: 'institutions:public:read',
  institutionsManage: 'institutions:public:manage',
  profileRead: 'professional:profile:read',
  profileUpdate: 'professional:profile:update',
  relationshipsRead: 'relationships:professional:read',
  relationshipsManage: 'relationships:professional:manage',
  ratesCreate: 'rates:hourly:create',
  periodsRead: 'periods:monthly:read',
  periodsManage: 'periods:monthly:manage',
  hoursManage: 'hours:record:manage',
  dashboardRead: 'dashboard:income:read',
  retentionRead: 'configuration:retention:read',
  privateLiquidationsRead: 'private-liquidations:read',
  privateLiquidationsCreate: 'private-liquidations:create',
  privateLiquidationsDelete: 'private-liquidations:delete',
} as const
