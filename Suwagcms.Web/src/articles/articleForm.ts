import type { Mistake } from "@/api/client";

import type { Article, SaveArticle } from "./api";

export type FieldName = "id" | "knowledgeBase" | "title" | "content" | "llmGuide" | "description";

export interface ArticleForm {
    id: string;
    knowledgeBase: string;
    title: string;
    content: string;
    llmGuide: string;
    description: string;
}

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

function kept(text: string, initial: string): string {
    return lineBreaksAsTyped(text) === lineBreaksAsTyped(initial) ? initial : text;
}

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

export function fieldOf(mistake: Mistake, articleId: string): FieldName | null {
    if (mistake.code === "resource.exists") return "id";
    const path =
        mistake.code === "reference.loop" && mistake.resource.id !== articleId
            ? mistake.related.find((item) => item.resource.kind === "Article" && item.resource.id === articleId)?.location
            : mistake.path;
    return FIELDS.find((field) => field === path) ?? null;
}
