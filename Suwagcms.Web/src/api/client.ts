import { ApiError, createApi, refusalReader } from "@maniumek/suwfrontkit";

import type { components, paths } from "./openapi";

export type Mistake = components["schemas"]["Mistake"];
type RefusalMistake = components["schemas"]["RefusalMistake"];
type RefusalCode = components["schemas"]["RefusalCode"];

export const api = createApi<paths>();

const refusals = refusalReader<RefusalCode, string>();

function shown(mistake: RefusalMistake): Mistake {
    const { at, ...rest } = mistake;
    return { ...rest, path: at ?? "" };
}

export function mistakesOf(error: unknown): Mistake[] {
    return refusals.mistakesOf(error).map((mistake) => shown(mistake as RefusalMistake));
}

export function refusalOf(error: unknown): ApiError {
    if (error instanceof ApiError) return error;
    return new ApiError(0, { detail: error instanceof Error ? error.message : String(error) });
}

export function messageOf(error: unknown): string {
    return error instanceof Error ? error.message : String(error);
}
