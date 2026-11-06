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
    id: "The article's id: how links and procedures refer to it. One camelCase word, like priceList. To change it later, use Change id.",
    title: "The name people see in lists and on link chips. A link's label holds it, so a new version fills in the current title.",
    knowledgeBase: "The base this article belongs to. The article takes its language from it.",
    language:
        "The language of the knowledge base the article is in, pl or en: the bot uses it to pick the right articles and to answer in the right way. To change it, move the article to another base or change the language of its base.",
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

function lineBreaksAsTyped(text: string): string {
    return text.replace(/\r\n?/g, "\n");
}

/** A textarea reports every line break as \n, so a text the person did not change is sent as it was stored, with its own breaks. */
function kept(text: string, initial: string): string {
    return lineBreaksAsTyped(text) === lineBreaksAsTyped(initial) ? initial : text;
}

/** The text is sent exactly as typed: the CMS neither trims nor reformats it. */
export function bodyOf(form: ArticleForm, initial: ArticleForm): SaveArticle {
    return {
        knowledgeBase: form.knowledgeBase,
        title: form.title,
        content: kept(form.content, initial.content),
        llmGuide: kept(form.llmGuide, initial.llmGuide),
        description: form.description,
    };
}

export function isDirty(form: ArticleForm, initial: ArticleForm): boolean {
    return FIELDS.some((field) => form[field] !== initial[field]);
}

/**
 * The field a mistake belongs beside: its path names it; a taken id is reported on `revision`, so it is told apart by its code.
 * A loop is reported once, on one article of it, and lists the others in `related`, so the article being edited finds its own link there.
 */
export function fieldOf(mistake: Mistake, articleId: string): FieldName | null {
    if (mistake.code === "resource.exists") return "id";
    const path =
        mistake.code === "reference.loop" && mistake.resource.id !== articleId
            ? mistake.related.find((item) => item.resource.kind === "Article" && item.resource.id === articleId)?.location
            : mistake.path;
    return FIELDS.find((field) => field === path) ?? null;
}
