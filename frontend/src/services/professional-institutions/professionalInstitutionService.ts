import { apiClient } from '../apiClient'
import type { ProfessionalInstitution } from '../../types/api'

export const professionalInstitutionService = {
  async list(professionalId?: string) {
    return (await apiClient.get<ProfessionalInstitution[]>('/professional-institutions', {
      params: professionalId ? { professionalId } : undefined,
    })).data
  },
  async add(institutionId: string) {
    return (await apiClient.post<ProfessionalInstitution>('/professional-institutions', { institutionId })).data
  },
  async createAndAdd(name: string) {
    return (await apiClient.post<ProfessionalInstitution>('/professional-institutions/quick-create', { name })).data
  },
  async deactivate(id: string) {
    await apiClient.delete(`/professional-institutions/${id}`)
  },
  async addRate(relationId: string, year: number, hourlyRateClp: number) {
    return (await apiClient.post(`/professional-institutions/${relationId}/rates`, { year, hourlyRateClp })).data
  },
}
