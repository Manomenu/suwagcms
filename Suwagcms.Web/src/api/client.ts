import { ApiError, createApi } from "@maniumek/suwfrontkit";

import type { components, paths } from "./openapi";

export type Mistake = components["schemas"]["Mistake"];

export const api = createApi<paths>();

export function mistakesOf(error: ApiError | null): Mistake[] {
    const mistakes = error?.problem["mistakes"];
    return Array.isArray(mistakes) ? (mistakes as Mistake[]) : [];
}

export function refusalOf(error: unknown): ApiError {
    if (error instanceof ApiError) return error;
    return new ApiError(0, { detail: error instanceof Error ? error.message : String(error) });
}

export function messageOf(error: unknown): string {
    return error instanceof Error ? error.message : String(error);
}
