import type { components } from "../api/openapi";
import { resourceApi } from "../api/resourceApi";

export type KnowledgeBase = components["schemas"]["KnowledgeBaseView"];
export type SaveKnowledgeBase = components["schemas"]["SaveKnowledgeBase"];

export const knowledgeBasesApi = resourceApi<KnowledgeBase, SaveKnowledgeBase>("knowledge-bases");
