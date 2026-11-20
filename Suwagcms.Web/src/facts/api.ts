import { dataOf } from "@maniumek/suwfrontkit";

import { api } from "../api/client";
import type { components } from "../api/openapi";
import type { ResourceApi } from "../api/resourceApi";

export type Fact = components["schemas"]["Fact"];
export type SaveFact = components["schemas"]["SaveFact"];

export const factsApi: ResourceApi<Fact, SaveFact> = {
    collection: "facts",
    list: (bot, signal) => dataOf(api.GET("/api/bots/{bot}/facts", { params: { path: { bot } }, signal })),
    get: (bot, name, signal) => dataOf(api.GET("/api/bots/{bot}/facts/{name}", { params: { path: { bot, name } }, signal })),
    save: (bot, name, body, ifRevision) =>
        dataOf(api.PUT("/api/bots/{bot}/facts/{name}", { params: { path: { bot, name }, query: { ifRevision } }, body })),
    remove: (bot, name, ifRevision) =>
        dataOf(api.DELETE("/api/bots/{bot}/facts/{name}", { params: { path: { bot, name }, query: { ifRevision } } })),
};
