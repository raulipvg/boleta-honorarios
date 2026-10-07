export interface AuthToken {
  accessToken: string
  expiresInSeconds: number
  requiresPasswordChange: boolean
}

export interface AuthIdentity {
  userId: string
  userName: string
  roleCodes: string[]
  permissionCodes: string[]
  requiresPasswordChange: boolean
}

export interface Institution {
  id: string
  name: string
  active: boolean
}

export interface ProfessionalSummary {
  id: string
  name: string
}

export interface HourlyRate {
  year: number
  version: number
  hourlyRateClp: number
  createdAt: string
}

export interface ProfessionalInstitution {
  id: string
  publicInstitutionId: string
  institutionName: string
  active: boolean
  rates: HourlyRate[]
}

export interface HourRecord {
  id: string
  hours: number
  order: number
  version: number
}

export interface SavedHourRecord {
  record: HourRecord
  workspace: MonthlyWorkspace
}

export interface HourRecordsPage {
  records: HourRecord[]
  hasMoreRecords: boolean
  nextBeforeOrder: number | null
}

export interface PeriodInstitution {
  professionalInstitutionId: string
  institutionName: string
  hourlyRateClp: number
  hourlyRateVersion: number
  totalHours: number
  grossTotalClp: number
  retentionTotalClp: number
  netTotalClp: number
  version: number
  recordsCount: number
  hasMoreRecords: boolean
  nextBeforeOrder: number | null
  records: HourRecord[]
}

export interface MonthlyWorkspace {
  periodId: string | null
  professionalId: string
  year: number
  month: number
  exists: boolean
  appliedRetentionPercentage: number | null
  totalHours: number
  grossTotalClp: number
  retentionTotalClp: number
  netTotalClp: number
  version: number
  institutions: PeriodInstitution[]
}

export interface DashboardInstitution {
  key: string
  name: string
  type: 'public' | 'private'
}

export interface DashboardInstitutionValue {
  institutionKey: string
  netTotalClp: number | null
}

export interface DashboardMonth {
  year: number
  month: number
  periodExists: boolean
  totalNetClp: number | null
  institutions: DashboardInstitutionValue[]
}

export interface DashboardData {
  professionalId: string
  fromYear: number
  toYear: number
  institutions: DashboardInstitution[]
  months: DashboardMonth[]
}

export interface AccountSummary {
  id: string
  userName: string
  active: boolean
  mustChangePassword: boolean
  roleCodes: string[]
  professionalName: string | null
}

export interface RetentionRate {
  year: number
  percentage: number
}

export interface CebienAttentionCount {
  serviceName: string
  count: number
}

export interface PrivateLiquidationPreview {
  sha256: string
  sourceType: 'pdf' | 'email'
  privateInstitutionName: string
  payerEntityId: string
  payerLegalName: string
  payerRut: string
  collectorRut: string | null
  reportedProfessionalName: string | null
  liquidationNumber: string | null
  liquidationDate: string | null
  serviceYear: number
  serviceMonth: number
  accountingYear: number
  accountingMonth: number
  fortnight: number | null
  paymentService: string
  executorName: string | null
  attentionCountsByService: CebienAttentionCount[]
  serviceTotalClp: number | null
  grossTotalClp: number
  appliedRetentionPercentage: number
  retentionTotalClp: number
  netTotalClp: number
  attentionCount: number
  reportedAttentionCount: number | null
  minutesPerAttention: number
  totalAttentionMinutes: number
  fileSizeBytes: number | null
  originalFileName: string | null
}

export interface PrivateLiquidation {
  id: string
  privateInstitutionName: string
  payerEntityId: string
  payerLegalName: string
  payerRut: string
  sourceType: 'pdf' | 'email'
  collectorRut: string | null
  reportedProfessionalName: string | null
  liquidationNumber: string | null
  liquidationDate: string | null
  serviceYear: number
  serviceMonth: number
  accountingYear: number
  accountingMonth: number
  fortnight: number | null
  paymentService: string
  executorName: string | null
  serviceTotalClp: number | null
  appliedRetentionPercentage: number
  grossTotalClp: number
  retentionTotalClp: number
  netTotalClp: number
  attentionCount: number
  reportedAttentionCount: number | null
  minutesPerAttention: number
  totalAttentionMinutes: number
  importedAt: string
}
