import { apiRequest } from "@/lib/apiClient";
import { ApiError } from "@/lib/ApiError";
import type { CurrentUserResponse, LoginRequest, UserProfileDto } from "@/types/api";

export function login(request: LoginRequest): Promise<UserProfileDto> {
  return apiRequest<UserProfileDto>("/api/auth/login", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });
}

export async function getCurrentUser(
  signal?: AbortSignal,
): Promise<CurrentUserResponse | null> {
  try {
    return await apiRequest<CurrentUserResponse>("/api/auth/me", {
      signal,
      cache: "no-store",
    });
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return null;
    }

    throw error;
  }
}

export function logout(): Promise<void> {
  return apiRequest<void>("/api/auth/logout", {
    method: "POST",
  });
}
