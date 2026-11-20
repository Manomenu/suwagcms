import { request } from "../api/client";
import type { components } from "../api/openapi";

export type Persona = components["schemas"]["PersonaView"];
export type SavePersona = components["schemas"]["SavePersona"];

function personaPath(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}/persona`;
}

export function fetchPersona(bot: string, signal: AbortSignal): Promise<Persona> {
    return request<Persona>(personaPath(bot), { signal });
}

export function savePersona(bot: string, body: SavePersona, ifRevision: number): Promise<Persona> {
    return request<Persona>(`${personaPath(bot)}?ifRevision=${ifRevision.toString()}`, { method: "PUT", body: JSON.stringify(body) });
}
