import type { Mistake } from "@/api/client";

export type FieldName = "id" | "title" | "language";

/** What the language of a base is for: the label's tooltip. */
export const HINTS = {
    language:
        "The language of every article in this base, pl or en. The bot uses it to pick the right articles and to answer in the right way.",
} as const;

/** What the person typed, so that a refusal never loses a keystroke. */
export interface KnowledgeBaseForm {
    id: string;
    title: string;
    language: string;
}

export function emptyForm(): KnowledgeBaseForm {
    return { id: "", title: "", language: "en" };
}

/** The field a mistake belongs beside: its path names it; a taken id is reported on `revision`, so it is told apart by its code. */
export function fieldOf(mistake: Mistake): FieldName | null {
    if (mistake.code === "resource.exists") return "id";
    return mistake.path === "id" || mistake.path === "title" || mistake.path === "language" ? mistake.path : null;
}

/** A repeated title is reported for every base that has it; the form shows only the one it is saving. */
export function mistakesFor(mistakes: Mistake[], id: string): Mistake[] {
    return mistakes.filter((mistake) => mistake.resource.kind !== "KnowledgeBase" || mistake.resource.id === id);
}
