/**
 * What every feature's HTTP calls share: the API's base path, `request`, and the error a response turns into.
 * The calls themselves live with their feature (`<feature>/api.ts`); their types come from the generated `openapi.d.ts`.
 */
import type { components } from "./openapi";

const BASE = import.meta.env.VITE_API_BASE ?? "/api";

export type Mistake = components["schemas"]["Mistake"];

/** The error a failed response carries: the server's ProblemDetails `detail`, and the `mistakes` of a refusal. */
export class ApiError extends Error {
    readonly mistakes: Mistake[];
    /** The HTTP status of the refusal; 0 when the error did not come from a response. */
    readonly status: number;

    constructor(message: string, mistakes: Mistake[] = [], status = 0) {
        super(message);
        this.mistakes = mistakes;
        this.status = status;
    }
}

export async function toError(response: Response): Promise<ApiError> {
    const body = (await response.json().catch(() => null)) as { detail?: unknown; mistakes?: unknown } | null;
    const detail = typeof body?.detail === "string" ? body.detail : null;
    const mistakes = Array.isArray(body?.mistakes) ? (body.mistakes as Mistake[]) : [];
    return new ApiError(detail ?? `${response.status} ${response.statusText}`, mistakes, response.status);
}

/** A JSON call to the API: the parsed body, or the error the response carries. */
export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const response = await fetch(`${BASE}${path}`, {
        ...init,
        headers: init.body === undefined ? {} : { "content-type": "application/json" },
    });
    if (!response.ok) throw await toError(response);
    return (response.status === 204 ? undefined : await response.json()) as T;
}
