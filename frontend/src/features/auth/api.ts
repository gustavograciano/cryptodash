import { apiClient } from '@/lib/apiClient';

export interface LoginResponse { token: string; expiresAt: string; }

export async function login(email: string, password: string): Promise<LoginResponse> {
  const { data } = await apiClient.post<LoginResponse>('/auth/login', { email, password });
  return data;
}
