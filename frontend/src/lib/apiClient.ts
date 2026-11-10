import type { ApiProblemDetails } from "@/types/api";
import { ApiError } from "./ApiError";
import { getAntiforgeryToken } from "./antiforgery";

function readProblem(value: unknown): ApiProblemDetails | undefined {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return undefined;
  }

  const data = value as Record<string, unknown>;
  const problem: ApiProblemDetails = {};

  if (typeof data.title === "string") problem.title = data.title;
  if (typeof data.detail === "string") problem.detail = data.detail;
  if (typeof data.traceId === "string") problem.traceId = data.traceId;

  if (
    typeof data.errors === "object" &&
    data.errors !== null &&
    !Array.isArray(data.errors)
  ) {
    const entries = Object.entries(data.errors).filter(
      (entry): entry is [string, string[]] =>
        Array.isArray(entry[1]) &&
        entry[1].every((message: unknown) => typeof message === "string"),
    );

    problem.errors = Object.fromEntries(entries);
  }

  return problem;
}

export async function apiRequest<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const method = (options.method ?? "GET").toUpperCase();

  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");

  const safeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

  if (!safeMethods.includes(method)) {
    const token = await getAntiforgeryToken(
      options.signal ?? undefined
    );

    headers.set(token.headerName, token.requestToken);
  }

  const response = await fetch(path, {
    ...options,
    method,
    credentials: "same-origin",
    headers,
  });

  if (!response.ok) {
    const text = await response.text();
    let problem: ApiProblemDetails | undefined;

    try {
      problem = readProblem(JSON.parse(text));
    } catch {
      //
    }

    throw new ApiError(response.status, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
