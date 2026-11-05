// The state of the creator and how a save changes it. The drawing is never replaced by a failed save: only a reload does that.
import type { Mistake } from "@/api/client";

import type { ProcedureDetail } from "./api";
import { revisionOfProcedure } from "./api";
import { ApiError } from "@/api/client";
import { reasonOf } from "./mistakes";
import { fromDetail, toSave, type Draft } from "./draft";

type Notice = { kind: "refused"; mistakes: Mistake[] } | { kind: "stale"; message: string } | { kind: "failed"; message: string };

export interface CreatorState {
    draft: Draft;
    /** The text of the draft as last saved or loaded; the draft differs from it exactly when there is something to save. */
    savedAs: string;
    revision: number;
    notReady: Mistake[];
    notice: Notice | null;
}

export type SaveOutcome = { kind: "saved"; detail: ProcedureDetail } | { kind: "notSaved"; notice: Notice };

export type Action =
    | { type: "edited"; draft: Draft }
    /** `sent` is the text of the draft the save carried: the author may have gone on editing while it was on its way. */
    | { type: "saved"; outcome: SaveOutcome; sent: string }
    | { type: "loaded"; detail: ProcedureDetail };

export function loaded(detail: ProcedureDetail): CreatorState {
    const draft = fromDetail(detail);
    return {
        draft,
        savedAs: JSON.stringify(toSave(draft)),
        revision: revisionOfProcedure(detail),
        notReady: detail.notReady,
        notice: null,
    };
}

export function isDirty(state: CreatorState): boolean {
    return JSON.stringify(toSave(state.draft)) !== state.savedAs;
}

export function reduce(state: CreatorState, action: Action): CreatorState {
    switch (action.type) {
        case "edited":
            return { ...state, draft: action.draft, notice: state.notice?.kind === "stale" ? state.notice : null };
        case "loaded":
            return loaded(action.detail);
        case "saved":
            return action.outcome.kind === "saved"
                ? {
                      ...state,
                      savedAs: action.sent,
                      revision: revisionOfProcedure(action.outcome.detail),
                      notReady: action.outcome.detail.notReady,
                      notice: null,
                  }
                : { ...state, notice: action.outcome.notice };
    }
}

/** Sorts the answer of a save: saved, refused with mistakes, based on an old revision, or failed. */
export async function outcomeOf(answer: Promise<ProcedureDetail>): Promise<SaveOutcome> {
    try {
        return { kind: "saved", detail: await answer };
    } catch (error) {
        if (error instanceof ApiError && error.status === 422)
            return { kind: "notSaved", notice: { kind: "refused", mistakes: error.mistakes } };
        if (error instanceof ApiError && error.status === 409)
            return { kind: "notSaved", notice: { kind: "stale", message: reasonOf(error) } };
        return {
            kind: "notSaved",
            notice: { kind: "failed", message: error instanceof Error ? error.message : String(error) },
        };
    }
}
