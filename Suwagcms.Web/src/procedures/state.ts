import type { Mistake } from "@/api/client";

import type { ProcedureDetail } from "./api";
import { revisionOfProcedure } from "./api";
import { ApiError } from "@/api/client";
import { reasonOf } from "./mistakes";
import { fromDetail, toSave, type Draft } from "./draft";

type Notice = { kind: "refused"; mistakes: Mistake[] } | { kind: "stale"; message: string } | { kind: "failed"; message: string };

export interface EditorState {
    draft: Draft;
    savedAs: string;
    revision: number;
    notReady: Mistake[];
    notice: Notice | null;
}

export type SaveOutcome = { kind: "saved"; detail: ProcedureDetail } | { kind: "notSaved"; notice: Notice };

export type Action =
    { type: "edited"; draft: Draft } | { type: "saved"; outcome: SaveOutcome; sent: string } | { type: "loaded"; detail: ProcedureDetail };

export function loaded(detail: ProcedureDetail): EditorState {
    const draft = fromDetail(detail);
    return {
        draft,
        savedAs: JSON.stringify(toSave(draft)),
        revision: revisionOfProcedure(detail),
        notReady: detail.notReady,
        notice: null,
    };
}

export function isDirty(state: EditorState): boolean {
    return JSON.stringify(toSave(state.draft)) !== state.savedAs;
}

export function reduce(state: EditorState, action: Action): EditorState {
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
