import { apiClient } from '../apiClient'
import type { MonthlyWorkspace } from '../../types/api'

export const periodService = {
  async workspace(year: number, month: number, professionalId?: string) {
    return (await apiClient.get<MonthlyWorkspace>(`/periods/${year}/${month}`, {
      params: professionalId ? { professionalId } : undefined,
    })).data
  },
  async addInstitution(year: number, month: number, professionalInstitutionId: string) {
    return (await apiClient.put<MonthlyWorkspace>(`/periods/${year}/${month}/institutions`, { professionalInstitutionId })).data
  },
  async removeInstitution(year: number, month: number, professionalInstitutionId: string) {
    return (await apiClient.delete<MonthlyWorkspace>(`/periods/${year}/${month}/institutions/${professionalInstitutionId}`)).data
  },
}
