// Why: every screen of a resource changes its id through the same route, so the call and what its answer means for the dialog exist once.
import { ApiError, request } from "./client";
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
    const query = `?ifRevision=${ifRevision}${dryRun ? "&dryRun=true" : ""}`;
    return request<RenameAnswer>(`/bots/${encodeURIComponent(bot)}/${collection}/${encodeURIComponent(id)}/rename${query}`, {
        method: "POST",
        body: JSON.stringify({ newId }),
        signal: signal ?? null,
    });
}

function linesOf(answer: RenameAnswer): { id: string; where: string }[] {
    return answer.referrers.map((referrer) => {
        const links = Number(referrer.links);
        const isText = referrer.field === "content" || referrer.field === "llmGuide";
        return { id: referrer.id, where: isText ? `${referrer.field}, ${links} ${links === 1 ? "link" : "links"}` : referrer.field };
    });
}

function refusalOf(error: unknown): { lines: { id: string; where: string }[]; mistakes: string[] } {
    if (error instanceof ApiError && error.mistakes.length > 0) {
        return { lines: [], mistakes: error.mistakes.map((mistake) => `${mistake.message} ${mistake.fix}`) };
    }
    return { lines: [], mistakes: [error instanceof Error ? error.message : String(error)] };
}

/** What the dialog asks of the server: a dry run for each id typed, then the change itself, both guarded by the revision the screen shows. */
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
