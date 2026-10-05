import { apiClient } from './apiClient'
import type {
  AccountSummary,
  DashboardData,
  Institution,
  HourRecordsPage,
  MonthlyWorkspace,
  ProfessionalInstitution,
  ProfessionalSummary,
  RetentionRate,
  SavedHourRecord,
} from '../types/api'

export const incomeService = {
  async dashboard(params: { fromYear: number; toYear: number; professionalId?: string; institutionIds?: string[] }) {
    return (await apiClient.get<DashboardData>('/dashboard', { params, paramsSerializer: { indexes: null } })).data
  },
  async professionals() {
    return (await apiClient.get<ProfessionalSummary[]>('/admin/professionals')).data
  },
  async institutions() {
    return (await apiClient.get<Institution[]>('/institutions')).data
  },
  async createInstitution(name: string) {
    return (await apiClient.post<Institution>('/institutions', { name })).data
  },
  async updateInstitution(id: string, name: string, active: boolean) {
    return (await apiClient.put<Institution>(`/institutions/${id}`, { name, active })).data
  },
  async profile() {
    return (await apiClient.get<{ id: string; userId: string; name: string }>('/profile')).data
  },
  async updateProfile(name: string) {
    return (await apiClient.put('/profile', { name })).data
  },
  async professionalInstitutions(professionalId?: string) {
    return (await apiClient.get<ProfessionalInstitution[]>('/professional-institutions', { params: professionalId ? { professionalId } : undefined })).data
  },
  async addProfessionalInstitution(institutionId: string) {
    return (await apiClient.post<ProfessionalInstitution>('/professional-institutions', { institutionId })).data
  },
  async createAndAddProfessionalInstitution(name: string) {
    return (await apiClient.post<ProfessionalInstitution>('/professional-institutions/quick-create', { name })).data
  },
  async deactivateProfessionalInstitution(id: string) {
    await apiClient.delete(`/professional-institutions/${id}`)
  },
  async addRate(relationId: string, year: number, hourlyRateClp: number) {
    return (await apiClient.post(`/professional-institutions/${relationId}/rates`, { year, hourlyRateClp })).data
  },
  async retentionRates() {
    return (await apiClient.get<RetentionRate[]>('/configuration/retention-rates')).data
  },
  async workspace(year: number, month: number, professionalId?: string) {
    return (await apiClient.get<MonthlyWorkspace>(`/periods/${year}/${month}`, { params: professionalId ? { professionalId } : undefined })).data
  },
  async olderHours(year: number, month: number, professionalInstitutionId: string, beforeOrder: number, professionalId?: string) {
    return (await apiClient.get<HourRecordsPage>(
      `/periods/${year}/${month}/institutions/${professionalInstitutionId}/hours`,
      { params: { beforeOrder, pageSize: 100, ...(professionalId ? { professionalId } : {}) } },
    )).data
  },
  async addInstitutionToPeriod(year: number, month: number, professionalInstitutionId: string) {
    return (await apiClient.put<MonthlyWorkspace>(`/periods/${year}/${month}/institutions`, { professionalInstitutionId })).data
  },
  async removeInstitutionFromPeriod(year: number, month: number, professionalInstitutionId: string) {
    return (await apiClient.delete<MonthlyWorkspace>(`/periods/${year}/${month}/institutions/${professionalInstitutionId}`)).data
  },
  async addHours(year: number, month: number, professionalInstitutionId: string, hours: number) {
    return (await apiClient.post<SavedHourRecord>(`/periods/${year}/${month}/institutions/${professionalInstitutionId}/hours`, { hours })).data
  },
  async updateHours(id: string, hours: number, version: number) {
    return (await apiClient.put<SavedHourRecord>(`/hours/${id}`, { hours, version })).data
  },
  async deleteHours(id: string, version: number) {
    return (await apiClient.delete<MonthlyWorkspace>(`/hours/${id}`, { params: { version } })).data
  },
  async users() {
    return (await apiClient.get<AccountSummary[]>('/admin/users')).data
  },
  async createUser(input: { userName: string; temporaryPassword: string; roleCodes: string[]; professionalName?: string }) {
    return (await apiClient.post<AccountSummary>('/admin/users', input)).data
  },
  async resetUserPassword(id: string, temporaryPassword: string) {
    await apiClient.post(`/admin/users/${id}/reset-password`, { temporaryPassword })
  },
}
