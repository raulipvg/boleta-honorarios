import { apiClient } from '../apiClient'
import type { HourRecordsPage, MonthlyWorkspace, SavedHourRecord } from '../../types/api'

export const hourRecordService = {
  async older(year: number, month: number, professionalInstitutionId: string, beforeOrder: number, professionalId?: string) {
    return (await apiClient.get<HourRecordsPage>(
      `/periods/${year}/${month}/institutions/${professionalInstitutionId}/hours`,
      { params: { beforeOrder, pageSize: 100, ...(professionalId ? { professionalId } : {}) } },
    )).data
  },
  async create(year: number, month: number, professionalInstitutionId: string, hours: number) {
    return (await apiClient.post<SavedHourRecord>(
      `/periods/${year}/${month}/institutions/${professionalInstitutionId}/hours`,
      { hours },
    )).data
  },
  async update(id: string, hours: number, version: number) {
    return (await apiClient.put<SavedHourRecord>(`/hours/${id}`, { hours, version })).data
  },
  async delete(id: string, version: number) {
    return (await apiClient.delete<MonthlyWorkspace>(`/hours/${id}`, { params: { version } })).data
  },
}
