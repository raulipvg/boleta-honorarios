import { apiClient } from '../apiClient'
import type { ProfessionalSummary } from '../../types/api'

export const professionalService = {
  async list() {
    return (await apiClient.get<ProfessionalSummary[]>('/admin/professionals')).data
  },
}
