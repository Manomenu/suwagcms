import { dataOf } from "@maniumek/suwfrontkit";

import { api } from "../api/client";
import type { components } from "../api/openapi";

export type Persona = components["schemas"]["PersonaView"];
export type SavePersona = components["schemas"]["SavePersona"];

export function fetchPersona(bot: string, signal: AbortSignal): Promise<Persona> {
    return dataOf(api.GET("/api/bots/{bot}/persona", { params: { path: { bot } }, signal }));
}

export function savePersona(bot: string, body: SavePersona, ifRevision: number): Promise<Persona> {
    return dataOf(api.PUT("/api/bots/{bot}/persona", { params: { path: { bot }, query: { ifRevision } }, body }));
}
