import { dataOf } from "@maniumek/suwfrontkit";

import { api } from "../api/client";
import type { components } from "../api/openapi";
import type { ResourceApi } from "../api/resourceApi";

export type KnowledgeBase = components["schemas"]["KnowledgeBaseView"];
export type SaveKnowledgeBase = components["schemas"]["SaveKnowledgeBase"];

export const knowledgeBasesApi: ResourceApi<KnowledgeBase, SaveKnowledgeBase> = {
    collection: "knowledge-bases",
    list: (bot, signal) => dataOf(api.GET("/api/bots/{bot}/knowledge-bases", { params: { path: { bot } }, signal })),
    get: (bot, id, signal) => dataOf(api.GET("/api/bots/{bot}/knowledge-bases/{id}", { params: { path: { bot, id } }, signal })),
    save: (bot, id, body, ifRevision) =>
        dataOf(api.PUT("/api/bots/{bot}/knowledge-bases/{id}", { params: { path: { bot, id }, query: { ifRevision } }, body })),
    remove: (bot, id, ifRevision) =>
        dataOf(api.DELETE("/api/bots/{bot}/knowledge-bases/{id}", { params: { path: { bot, id }, query: { ifRevision } } })),
};
