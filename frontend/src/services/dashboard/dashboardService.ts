import { apiClient } from '../apiClient'
import type { DashboardData } from '../../types/api'

export const dashboardService = {
  async dashboard(params: { fromYear: number; toYear: number; professionalId?: string; institutionIds?: string[] }) {
    return (await apiClient.get<DashboardData>('/dashboard', { params, paramsSerializer: { indexes: null } })).data
  },
}
