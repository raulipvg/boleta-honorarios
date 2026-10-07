import { apiClient } from '../apiClient'
import type { PrivateLiquidation, PrivateLiquidationListItem, PrivateLiquidationPreview } from '../../types/api'

function toFormData(file: File, minutesPerAttention: number): FormData {
  const form = new FormData()
  form.append('file', file)
  form.append('minutesPerAttention', String(minutesPerAttention))
  return form
}

export const privateLiquidationService = {
  async preview(file: File, minutesPerAttention: number) {
    return (await apiClient.post<PrivateLiquidationPreview>(
      '/private-liquidations/preview', toFormData(file, minutesPerAttention),
      { headers: { 'Content-Type': 'multipart/form-data' } },
    )).data
  },

  async import(file: File, minutesPerAttention: number, expectedSha256: string, expectedRetentionPercentage: number) {
    const form = toFormData(file, minutesPerAttention)
    form.append('expectedSha256', expectedSha256)
    form.append('expectedRetentionPercentage', String(expectedRetentionPercentage))
    return (await apiClient.post<PrivateLiquidation>(
      '/private-liquidations', form, { headers: { 'Content-Type': 'multipart/form-data' } },
    )).data
  },

  async previewCebien(emailBody: string, accountingYear: number, accountingMonth: number, minutesPerAttention: number) {
    return (await apiClient.post<PrivateLiquidationPreview>('/private-liquidations/cebien/preview', {
      emailBody,
      accountingYear,
      accountingMonth,
      minutesPerAttention,
    })).data
  },

  async importCebien(emailBody: string, accountingYear: number, accountingMonth: number,
    minutesPerAttention: number, expectedSha256: string, expectedRetentionPercentage: number) {
    return (await apiClient.post<PrivateLiquidation>('/private-liquidations/cebien', {
      emailBody,
      accountingYear,
      accountingMonth,
      minutesPerAttention,
      expectedSha256,
      expectedRetentionPercentage,
    })).data
  },

  async list(params: { year?: number; month?: number; professionalId?: string }) {
    return (await apiClient.get<PrivateLiquidationListItem[]>('/private-liquidations', { params })).data
  },

  async download(id: string) {
    return (await apiClient.get<Blob>(`/private-liquidations/${id}/file`, { responseType: 'blob' })).data
  },

  async source(id: string) {
    return (await apiClient.get<string>(`/private-liquidations/${id}/source`, { responseType: 'text' })).data
  },

  async delete(id: string) {
    await apiClient.delete(`/private-liquidations/${id}`)
  },
}
