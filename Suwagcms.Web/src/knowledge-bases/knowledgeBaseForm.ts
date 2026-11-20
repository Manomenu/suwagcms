import type { Mistake } from "@/api/client";
import { whole } from "@/api/constraints";

import type { KnowledgeBase, SaveKnowledgeBase } from "./api";

export type FieldName = "id" | "title" | "language" | "embedding" | "results";

export const HINTS = {
    id: "The base's id: how its articles refer to it. One camelCase word, like offer. To change it later, use Change id.",
    title: "The name people see in lists. No two bases of a bot may share it.",
    language:
        "The language of every article in this base, pl or en. The bot uses it to pick the right articles and to answer in the right way.",
    embedding:
        "The name of the embedding profile the bot reads the articles of this base with, such as default. Its model and address are set on the bot, never here; a name the bot does not serve is refused when a version is created.",
    results: "How many passages one search gives from this base, a whole number from 1 to 20. The bot reads them to answer a question.",
} as const;

export interface KnowledgeBaseForm {
    id: string;
    title: string;
    language: string;
    embedding: string;
    results: string;
}

export function emptyForm(): KnowledgeBaseForm {
    return { id: "", title: "", language: "en", embedding: "default", results: "5" };
}

export function formOf(base: KnowledgeBase): KnowledgeBaseForm {
    return { id: base.id, title: base.title, language: base.language, embedding: base.embedding, results: String(base.results) };
}

export function saveOf(form: KnowledgeBaseForm): SaveKnowledgeBase {
    return { title: form.title, language: form.language, embedding: form.embedding, results: whole(form.results) };
}

export function fieldOf(mistake: Mistake): FieldName | null {
    if (mistake.code === "resource.exists") return "id";
    return mistake.path === "id" ||
        mistake.path === "title" ||
        mistake.path === "language" ||
        mistake.path === "embedding" ||
        mistake.path === "results"
        ? mistake.path
        : null;
}

export function mistakesFor(mistakes: Mistake[], id: string): Mistake[] {
    return mistakes.filter((mistake) => mistake.resource.kind !== "KnowledgeBase" || mistake.resource.id === id);
}
