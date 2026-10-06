import { apiClient } from '../apiClient'
import type { RetentionRate } from '../../types/api'

export const retentionService = {
  async list() {
    return (await apiClient.get<RetentionRate[]>('/configuration/retention-rates')).data
  },
}
