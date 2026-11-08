// What the list of mistakes shows. The mistakes come from the server's dry run; this file only words and sorts them.
import { ApiError, type Mistake } from "@/api/client";

import type { ProcedureDetail } from "./api";
import type { Draft } from "./draft";

/** The words of a refusal: the first mistake with its fix, since the response's own detail only counts them. */
export function reasonOf(error: ApiError): string {
    const first = error.mistakes[0];
    return first === undefined ? error.message : `${first.message} ${first.fix}`;
}

export type Check =
    | { state: "checking" }
    | { state: "done"; mistakes: Mistake[] }
    | { state: "stale"; message: string }
    | { state: "unreachable"; message: string };

/** Turns the answer of a dry run into what the list shows. A refusal carries the mistakes that would refuse a save. */
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

/** The place on the page a mistake belongs beside. A mistake with no place on the page goes at the top of the form, so nothing is lost. */
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
    return `step:${name}`;
}

/** The step to select for a place, so clicking a mistake opens the row it is about. */
export function stepOfPlace(place: string): string | null {
    const match = /^(?:step|failure|knowledge):([^:]+)/.exec(place);
    return match?.[1] ?? null;
}

/** The If or Else if row to unfold for a place, so clicking a mistake beside a branch opens that row and not its step. */
export function conditionOfPlace(place: string): string | null {
    return /^branch:(.+)$/.exec(place)?.[1] ?? null;
}
