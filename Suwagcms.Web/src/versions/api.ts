// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type VersionSummary = components["schemas"]["VersionSummary"];
export type VersionDetail = components["schemas"]["VersionDetail"];
export type VersionCreated = components["schemas"]["VersionCreated"];
export type VersionComparison = components["schemas"]["VersionComparison"];
export type ResourceDifference = components["schemas"]["ResourceDifference"];

function versionsPath(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}/versions`;
}

export function fetchVersions(bot: string, signal: AbortSignal): Promise<VersionSummary[]> {
    return request<VersionSummary[]>(versionsPath(bot), { signal });
}

export function fetchVersion(bot: string, number: number, signal: AbortSignal): Promise<VersionDetail> {
    return request<VersionDetail>(`${versionsPath(bot)}/${number}`, { signal });
}

export function fetchComparison(bot: string, from: number, to: number, signal: AbortSignal): Promise<VersionComparison> {
    return request<VersionComparison>(`${versionsPath(bot)}/${from}/diff/${to}`, { signal });
}

export function createVersion(bot: string): Promise<VersionCreated> {
    return request<VersionCreated>(versionsPath(bot), { method: "POST" });
}

/** The generated type allows a string for an int32; the server sends a number. */
export function numberOf(version: VersionSummary): number {
    return Number(version.number);
}

// Why: the API takes a version as a 32-bit number, so anything longer in an address is no version at all rather than a refused request.
const LARGEST_NUMBER = 2_147_483_647;

/** The version an address asks for: a whole number, 1 or more; anything else is none. */
export function versionNumberOf(raw: string | null): number | null {
    if (raw === null || !/^[1-9]\d*$/.test(raw)) return null;
    const number = Number(raw);
    return number <= LARGEST_NUMBER ? number : null;
}

export function dateOf(version: VersionSummary): string {
    return version.createdAt.slice(0, 16).replace("T", " ");
}

export function contentOf(version: VersionSummary): string {
    return `${version.facts} facts, ${version.knowledgeBases} knowledge bases, ${version.articles} articles, ${version.procedures} procedures, ${version.actions} actions`;
}

export type BotStatus = components["schemas"]["BotStatus"];
export type PushResult = components["schemas"]["PushResult"];
export type PushPlan = components["schemas"]["PushPlan"];

export function fetchStatus(bot: string, signal: AbortSignal): Promise<BotStatus> {
    return request<BotStatus>(`${versionsPath(bot)}/status`, { signal });
}

export function pushVersion(bot: string, number: number): Promise<PushResult> {
    return request<PushResult>(`${versionsPath(bot)}/${number}/push`, { method: "POST" });
}

export function fetchPushPlan(bot: string, number: number): Promise<PushPlan> {
    return request<PushPlan>(`${versionsPath(bot)}/${number}/push-plan`);
}
