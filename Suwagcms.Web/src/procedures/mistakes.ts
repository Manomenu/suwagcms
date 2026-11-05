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

/** What clicking a mistake selects: its step, or the panel of the procedure when it belongs to no step. */
export function targetOf(mistake: Mistake, draft: Draft): string | null {
    const name = /^steps\.([^.[]+)/.exec(mistake.path)?.[1];
    return name !== undefined && name in draft.steps ? name : null;
}

export function stepsWithMistakes(mistakes: Mistake[], draft: Draft): Set<string> {
    return new Set(mistakes.flatMap((mistake) => targetOf(mistake, draft) ?? []));
}
