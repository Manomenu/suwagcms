import { ApiError, type Mistake } from "@/api/client";

import type { ProcedureDetail } from "./api";
import type { Draft } from "./draft";

export function reasonOf(error: ApiError): string {
    const first = error.mistakes[0];
    return first === undefined ? error.message : `${first.message} ${first.fix}`;
}

export type Check =
    | { state: "checking" }
    | { state: "done"; mistakes: Mistake[] }
    | { state: "stale"; message: string }
    | { state: "unreachable"; message: string };

export async function checkOf(answer: Promise<ProcedureDetail>): Promise<Check> {
    try {
        return { state: "done", mistakes: (await answer).notReady };
    } catch (error) {
        if (error instanceof ApiError && error.status === 422) return { state: "done", mistakes: error.mistakes };
        if (error instanceof ApiError && error.status === 409) return { state: "stale", message: reasonOf(error) };
        if (error instanceof ApiError && error.status !== 0)
            return { state: "unreachable", message: `The server answered: ${reasonOf(error)}` };
        return { state: "unreachable", message: "The server could not be reached, so the mistakes below may be out of date." };
    }
}

export function consequenceOf(mistake: Mistake): string {
    return mistake.stage === "Save" ? "Refuses a save" : "Stops a version";
}

export function placeOf(mistake: Mistake, procedure: string, draft: Draft): string {
    if (mistake.resource.kind !== "Procedure" || mistake.resource.id !== procedure) return "top";
    const path = mistake.path;
    const field = /^(description|whenStalled|whenInterrupted|localFacts)\b/.exec(path)?.[1];
    if (field !== undefined) return `field:${field}`;
    const name = /^steps\.([^.[]+)/.exec(path)?.[1];
    if (name === undefined || !(name in draft.steps)) return "top";
    const knowledge = new RegExp(`^steps\\.${name}\\.inform\\.knowledge\\[(\\d+)\\]`).exec(path);
    if (knowledge) return `knowledge:${name}:${knowledge[1]}`;
    const branch = new RegExp(`^steps\\.${name}\\.next\\[(\\d+)\\]`).exec(path);
    if (branch) return `branch:${name}:${branch[1]}`;
    if (path === `steps.${name}.onFailure`) return `failure:${name}`;
    if (path === `steps.${name}.onUnavailable`) return `unavailable:${name}`;
    return `step:${name}`;
}

export function stepOfPlace(place: string): string | null {
    const match = /^(?:step|failure|unavailable|knowledge):([^:]+)/.exec(place);
    return match?.[1] ?? null;
}

export function conditionOfPlace(place: string): string | null {
    return /^branch:(.+)$/.exec(place)?.[1] ?? null;
}
