import type { Mistake } from "@/api/client";

import type { Article, SaveArticle } from "./api";

export type FieldName = "id" | "knowledgeBase" | "title" | "content" | "llmGuide" | "description";

/** What the person typed, so that a refusal never loses a keystroke. */
export interface ArticleForm {
    id: string;
    knowledgeBase: string;
    title: string;
    content: string;
    llmGuide: string;
    description: string;
}

/** Who reads each text and what it is for: the label's tooltip. */
export const HINTS = {
    content: "What is true. The customer reads it, through the model.",
    llmGuide: "How the model should talk about it: tone, what to ask first, what not to say. Only the model reads it.",
    description: "The bot's search reads this: say what the article answers. The customer never sees it.",
} as const;

const FIELDS: readonly FieldName[] = ["id", "knowledgeBase", "title", "content", "llmGuide", "description"];

export function emptyForm(knowledgeBase: string): ArticleForm {
    return { id: "", knowledgeBase, title: "", content: "", llmGuide: "", description: "" };
}

export function formOf(article: Article): ArticleForm {
    return {
        id: article.id,
        knowledgeBase: article.knowledgeBase,
        title: article.title,
        content: article.content,
        llmGuide: article.llmGuide,
        description: article.description,
    };
}

/** The text is sent exactly as typed: the CMS neither trims nor reformats it. */
export function bodyOf(form: ArticleForm): SaveArticle {
    return {
        knowledgeBase: form.knowledgeBase,
        title: form.title,
        content: form.content,
        llmGuide: form.llmGuide,
        description: form.description,
    };
}

export function isDirty(form: ArticleForm, initial: ArticleForm): boolean {
    return FIELDS.some((field) => form[field] !== initial[field]);
}

/** The field a mistake belongs beside: its path names it; a taken id is reported on `revision`, so it is told apart by its code. */
export function fieldOf(mistake: Mistake): FieldName | null {
    if (mistake.code === "resource.exists") return "id";
    return FIELDS.find((field) => field === mistake.path) ?? null;
}
