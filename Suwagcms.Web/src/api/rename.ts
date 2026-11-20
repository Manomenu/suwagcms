import { ApiError, dataOf } from "@maniumek/suwfrontkit";

import { api, mistakesOf } from "./client";
import type { components } from "./openapi";

type RenameAnswer = components["schemas"]["RenameAnswer"];
type RenameCollection = "facts" | "knowledge-bases" | "articles" | "procedures" | "actions";

function send(
    bot: string,
    collection: RenameCollection,
    id: string,
    newId: string,
    ifRevision: number,
    dryRun: boolean,
    signal?: AbortSignal,
) {
    const query = dryRun ? { ifRevision, dryRun } : { ifRevision };
    return dataOf(
        api.POST("/api/bots/{bot}/{kind}/{id}/rename", {
            params: { path: { bot, kind: collection, id }, query },
            body: { newId },
            signal: signal ?? null,
        }),
    );
}

function linesOf(answer: RenameAnswer): { id: string; where: string }[] {
    return answer.referrers.map((referrer) => {
        const links = Number(referrer.links);
        const isText = referrer.field === "content" || referrer.field === "llmGuide";
        return { id: referrer.id, where: isText ? `${referrer.field}, ${links} ${links === 1 ? "link" : "links"}` : referrer.field };
    });
}

function refusalOf(error: unknown): { lines: { id: string; where: string }[]; mistakes: string[] } {
    if (error instanceof ApiError && mistakesOf(error).length > 0) {
        return { lines: [], mistakes: mistakesOf(error).map((mistake) => `${mistake.message} ${mistake.fix}`) };
    }
    return { lines: [], mistakes: [error instanceof Error ? error.message : String(error)] };
}

export function renamer(bot: string, collection: RenameCollection, id: string, ifRevision: number) {
    const answered = (newId: string, dryRun: boolean, signal?: AbortSignal) =>
        send(bot, collection, id, newId, ifRevision, dryRun, signal).then(
            (answer) => ({ lines: linesOf(answer), mistakes: [] }),
            (error: unknown) => refusalOf(error),
        );
    return {
        preview: (newId: string, signal: AbortSignal) => answered(newId, true, signal),
        apply: (newId: string) => answered(newId, false),
    };
}
