import type { ApiProblemDetails } from "@/types/api";

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ApiProblemDetails | undefined;

  constructor(status: number, problem?: ApiProblemDetails) {
    const message =
      status >= 500
        ? "The server couldn't complete your request. Please try again."
        : (problem?.detail ??
          problem?.title ??
          `Request failed with status ${status}.`);

    super(message);

    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }
}
