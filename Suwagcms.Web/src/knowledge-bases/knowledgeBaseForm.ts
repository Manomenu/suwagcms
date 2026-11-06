import type { Mistake } from "@/api/client";

export type FieldName = "id" | "title" | "language";

/** What each field of a base is for: its tooltip. */
export const HINTS = {
    id: "The base's id: how its articles refer to it. One camelCase word, like offer. To change it later, use Change id.",
    title: "The name people see in lists. No two bases of a bot may share it.",
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
