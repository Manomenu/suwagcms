// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type VersionSummary = components["schemas"]["VersionSummary"];
export type VersionDetail = components["schemas"]["VersionDetail"];
export type VersionCreated = components["schemas"]["VersionCreated"];

function versionsPath(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}/versions`;
}

export function fetchVersions(bot: string, signal: AbortSignal): Promise<VersionSummary[]> {
    return request<VersionSummary[]>(versionsPath(bot), { signal });
}

export function fetchVersion(bot: string, number: number): Promise<VersionDetail> {
    return request<VersionDetail>(`${versionsPath(bot)}/${number}`);
}

export function createVersion(bot: string): Promise<VersionCreated> {
    return request<VersionCreated>(versionsPath(bot), { method: "POST" });
}

/** The generated type allows a string for an int32; the server sends a number. */
export function numberOf(version: VersionSummary): number {
    return Number(version.number);
}

export type BotStatus = components["schemas"]["BotStatus"];
export type PushResult = components["schemas"]["PushResult"];

export function fetchStatus(bot: string, signal: AbortSignal): Promise<BotStatus> {
    return request<BotStatus>(`${versionsPath(bot)}/status`, { signal });
}

export function pushVersion(bot: string, number: number): Promise<PushResult> {
    return request<PushResult>(`${versionsPath(bot)}/${number}/push`, { method: "POST" });
}
