import { apiClient } from '../apiClient'

export const profileService = {
  async get() {
    return (await apiClient.get<{ id: string; userId: string; name: string }>('/profile')).data
  },
  async update(name: string) {
    return (await apiClient.put('/profile', { name })).data
  },
}
