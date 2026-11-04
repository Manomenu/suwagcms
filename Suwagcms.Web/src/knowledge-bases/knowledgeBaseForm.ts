import type { Mistake } from "@/api/client";

export type FieldName = "id" | "title";

/** What the person typed, so that a refusal never loses a keystroke. */
export interface KnowledgeBaseForm {
    id: string;
    title: string;
}

export function emptyForm(): KnowledgeBaseForm {
    return { id: "", title: "" };
}

/** The field a mistake belongs beside: its path names it; a taken id is reported on `revision`, so it is told apart by its code. */
export function fieldOf(mistake: Mistake): FieldName | null {
    if (mistake.code === "resource.exists") return "id";
    return mistake.path === "id" || mistake.path === "title" ? mistake.path : null;
}

/** A repeated title is reported for every base that has it; the form shows only the one it is saving. */
export function mistakesFor(mistakes: Mistake[], id: string): Mistake[] {
    return mistakes.filter((mistake) => mistake.resource.kind !== "KnowledgeBase" || mistake.resource.id === id);
}
