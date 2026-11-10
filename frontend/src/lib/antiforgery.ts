import type { AntiforgeryTokenResponse } from "@/types/api";
import { ApiError } from "./ApiError";

export async function getAntiforgeryToken(
  signal?: AbortSignal,
): Promise<AntiforgeryTokenResponse> {
  const response = await fetch("/api/auth/csrf", {
    method: "GET",
    credentials: "same-origin",
    cache: "no-store",
    headers: {
      Accept: "application/json",
    },
    signal,
  });

  if (!response.ok) {
    throw new ApiError(response.status);
  }

  const token: unknown = await response.json();

  if (
    typeof token !== "object" ||
    token === null ||
    !("requestToken" in token) ||
    typeof token.requestToken !== "string" ||
    token.requestToken.trim().length === 0 ||
    !("headerName" in token) ||
    token.headerName !== "X-CSRF-TOKEN"
  ) {
    throw new Error("The server returned an invalid antiforgery response.");
  }

  return {
    requestToken: token.requestToken,
    headerName: token.headerName,
  };
}
