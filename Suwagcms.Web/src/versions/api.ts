import { dataOf } from "@maniumek/suwfrontkit";

import { api } from "../api/client";
import type { components } from "../api/openapi";

export type VersionSummary = components["schemas"]["VersionSummary"];
export type VersionDetail = components["schemas"]["VersionDetail"];
export type VersionCreated = components["schemas"]["VersionCreated"];
export type VersionComparison = components["schemas"]["VersionComparison"];
export type ResourceDifference = components["schemas"]["ResourceDifference"];

export function fetchVersions(bot: string, signal: AbortSignal): Promise<VersionSummary[]> {
    return dataOf(api.GET("/api/bots/{bot}/versions", { params: { path: { bot } }, signal }));
}

export function fetchVersion(bot: string, number: number, signal: AbortSignal): Promise<VersionDetail> {
    return dataOf(api.GET("/api/bots/{bot}/versions/{number}", { params: { path: { bot, number } }, signal }));
}

export function fetchComparison(bot: string, from: number, to: number, signal: AbortSignal): Promise<VersionComparison> {
    return dataOf(api.GET("/api/bots/{bot}/versions/{from}/diff/{to}", { params: { path: { bot, from, to } }, signal }));
}

export function createVersion(bot: string): Promise<VersionCreated> {
    return dataOf(api.POST("/api/bots/{bot}/versions", { params: { path: { bot } } }));
}

export function numberOf(version: VersionSummary): number {
    return Number(version.number);
}

const LARGEST_NUMBER = 2_147_483_647;

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
    return dataOf(api.GET("/api/bots/{bot}/versions/status", { params: { path: { bot } }, signal }));
}

export function pushVersion(bot: string, number: number): Promise<PushResult> {
    return dataOf(api.POST("/api/bots/{bot}/versions/{number}/push", { params: { path: { bot, number } } }));
}

export function fetchPushPlan(bot: string, number: number): Promise<PushPlan> {
    return dataOf(api.GET("/api/bots/{bot}/versions/{number}/push-plan", { params: { path: { bot, number } } }));
}
