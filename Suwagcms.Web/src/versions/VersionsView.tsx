import { Link } from "wouter";

import type { Mistake } from "@/api/client";
import { Button } from "@/design/button";

import { numberOf, type BotStatus, type PushResult, type VersionDetail, type VersionSummary } from "./api";
import { statusView, whenOf } from "./pushStatus";

/** The Resource kinds a mistake can name, and the screen that edits each; the bot as a whole (no conversation) is mended among the procedures. */
const SCREENS: Record<Mistake["resource"]["kind"], string> = {
    GlobalFact: "facts",
    KnowledgeBase: "knowledge-bases",
    Article: "articles",
    Procedure: "procedures",
    Action: "procedures",
    Definition: "procedures",
};

function screenOf(mistake: Mistake): string {
    return SCREENS[mistake.resource.kind];
}

function dateOf(version: VersionSummary): string {
    return version.createdAt.slice(0, 16).replace("T", " ");
}

function digestOf(sha256: string): string {
    return `${sha256.slice(0, 4)}…${sha256.slice(-4)}`;
}

/** The Versions screen: create a version, the mistakes that stopped one with a link to each resource, and the versions, newest first. */
export function VersionsView({
    bot,
    versions,
    status,
    pushFailure,
    pushing,
    refused,
    shown,
    onPush,
    onCreate,
    onOpen,
    onClose,
}: {
    bot: string;
    versions: VersionSummary[];
    status: BotStatus | null;
    pushFailure: PushResult | null;
    pushing: boolean;
    refused: Mistake[] | null;
    shown: VersionDetail | null;
    onPush: (number: number) => void;
    onCreate: () => void;
    onOpen: (version: VersionSummary) => void;
    onClose: () => void;
}) {
    const base = `~/bots/${encodeURIComponent(bot)}`;
    const bots = status === null ? null : statusView(status);
    return (
        <main className="mx-auto flex max-w-2xl flex-col gap-4 p-4">
            <header className="flex items-center gap-2">
                <h1 className="flex-1 text-2xl font-semibold">Versions of {bot}</h1>
                <Link href={base} className="text-sm text-primary underline underline-offset-4">
                    Bots
                </Link>
                <Button variant={bots?.push ? "outline" : "default"} onClick={onCreate}>
                    Create version
                </Button>
            </header>
            {status !== null && bots !== null && (
                <section className="flex flex-col gap-2 rounded-lg border p-3">
                    <div className="flex items-center gap-3">
                        <p className={`flex-1 font-medium ${bots.problem ? "text-destructive" : ""}`}>{bots.headline}</p>
                        {bots.push && (
                            <Button
                                disabled={pushing}
                                onClick={() => {
                                    if (bots.push) onPush(bots.push.number);
                                }}
                            >
                                {pushing ? "Pushing…" : bots.push.label}
                            </Button>
                        )}
                    </div>
                    {bots.notes.map((note) => (
                        <p key={note} className="text-sm text-muted-foreground">
                            {note}
                        </p>
                    ))}
                    {bots.inUse.length > 0 && (
                        <p className="text-sm text-muted-foreground">
                            Still in use by conversations: {bots.inUse.map((number) => `v${number}`).join(", ")}
                        </p>
                    )}
                    {pushFailure !== null && (
                        <div role="alert" className="flex flex-col gap-1 rounded-lg border border-destructive p-3 text-sm text-destructive">
                            <p className="font-medium">failed</p>
                            <p>
                                Push of v{Number(pushFailure.version)}: {pushFailure.reason}
                            </p>
                            <ul className="flex flex-col gap-1">
                                {pushFailure.mistakes.map((mistake, index) => (
                                    <li key={`${mistake.code}-${index}`}>
                                        {mistake.message} {mistake.fix}
                                    </li>
                                ))}
                            </ul>
                        </div>
                    )}
                    {status.attempts.length > 0 && (
                        <div className="flex flex-col gap-1 text-sm">
                            <p className="font-medium">Recent pushes</p>
                            <ul className="flex flex-col gap-1">
                                {status.attempts.map((attempt) => (
                                    <li
                                        key={attempt.id}
                                        className={attempt.outcome === "Failed" ? "text-destructive" : "text-muted-foreground"}
                                    >
                                        {whenOf(attempt.at)} · v{Number(attempt.version)} ·{" "}
                                        {attempt.outcome === "Failed" ? "failed" : "pushed"}
                                        {attempt.error === null ? "" : `: ${attempt.error}`}
                                    </li>
                                ))}
                            </ul>
                        </div>
                    )}
                </section>
            )}
            {refused !== null && (
                <div role="alert" className="flex flex-col gap-1 rounded-lg border border-destructive p-3 text-sm text-destructive">
                    <p className="font-medium">
                        Not created, {refused.length} {refused.length === 1 ? "mistake" : "mistakes"}
                    </p>
                    <ul className="flex flex-col gap-1">
                        {refused.map((mistake, index) => (
                            <li key={`${mistake.code}-${mistake.resource.id}-${mistake.path}-${index}`}>
                                {mistake.message} {mistake.fix}{" "}
                                <Link href={`${base}/${screenOf(mistake)}`} className="underline underline-offset-4">
                                    {mistake.resource.id === "" ? "Open" : `Open ${mistake.resource.id}`}
                                </Link>
                            </li>
                        ))}
                    </ul>
                </div>
            )}
            {versions.length === 0 ? (
                <p className="text-muted-foreground">No versions yet. Create one: the whole working state is checked and frozen.</p>
            ) : (
                <ul className="flex flex-col gap-2">
                    {versions.map((version) => (
                        <li key={numberOf(version)} className="flex items-center gap-3 rounded-lg border p-3">
                            <div className="flex flex-1 flex-col gap-1">
                                <span className="font-medium">
                                    v{numberOf(version)} · {dateOf(version)}
                                </span>
                                <span className="text-sm text-muted-foreground">
                                    {version.facts} facts, {version.knowledgeBases} knowledge bases, {version.articles} articles,{" "}
                                    {version.procedures} procedures
                                </span>
                            </div>
                            <code className="text-sm text-muted-foreground" title={version.sha256}>
                                {digestOf(version.sha256)}
                            </code>
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                    onOpen(version);
                                }}
                            >
                                Open
                            </Button>
                        </li>
                    ))}
                </ul>
            )}
            {shown !== null && (
                <section className="flex flex-col gap-2 rounded-lg border p-3">
                    <header className="flex items-center gap-2">
                        <h2 className="flex-1 font-medium">Version {numberOf(shown.version)}</h2>
                        <Button variant="outline" size="sm" onClick={onClose}>
                            Close
                        </Button>
                    </header>
                    <p className="text-sm text-muted-foreground">sha256 {shown.version.sha256}</p>
                    <pre className="max-h-96 overflow-auto text-xs whitespace-pre-wrap">{shown.document}</pre>
                </section>
            )}
        </main>
    );
}
