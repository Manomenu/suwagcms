// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import type { components } from "../api/openapi";
import { resourceApi } from "../api/resourceApi";

export type KnowledgeBase = components["schemas"]["KnowledgeBaseView"];
export type SaveKnowledgeBase = components["schemas"]["SaveKnowledgeBase"];

export const knowledgeBasesApi = resourceApi<KnowledgeBase, SaveKnowledgeBase>("knowledge-bases");
