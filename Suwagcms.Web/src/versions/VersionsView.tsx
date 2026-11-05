import type { Mistake } from "@/api/client";
import { Alert } from "@/design/alert";
import { Anchor } from "@/design/anchor";
import { Button } from "@/design/button";
import { Page, Paper } from "@/design/page";
import { Table } from "@/design/table";

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
        <Page
            title={`Versions of ${bot}`}
            actions={
                <Button variant={bots?.push ? "outline" : "default"} onClick={onCreate}>
                    Create version
                </Button>
            }
        >
            {status !== null && bots !== null && (
                <Paper className="flex flex-col gap-2">
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
                        <Alert variant="destructive">
                            <div className="flex flex-col gap-1">
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
                        </Alert>
                    )}
                    {status.attempts.length > 0 && (
                        <div className="flex flex-col gap-1 text-sm">
                            <p className="font-medium">Recent pushes</p>
                            <ul className="flex flex-col gap-1">
                                {status.attempts.map((attempt) => (
                                    <li key={attempt.id} className={attempt.outcome === "Failed" ? "text-destructive" : "text-success"}>
                                        {whenOf(attempt.at)} · v{Number(attempt.version)} ·{" "}
                                        {attempt.outcome === "Failed" ? "failed" : "pushed"}
                                        {attempt.error === null ? "" : `: ${attempt.error}`}
                                    </li>
                                ))}
                            </ul>
                        </div>
                    )}
                </Paper>
            )}
            {refused !== null && (
                <Alert variant="destructive">
                    <div className="flex flex-col gap-1">
                        <p className="font-medium">
                            Not created, {refused.length} {refused.length === 1 ? "mistake" : "mistakes"}
                        </p>
                        <ul className="flex flex-col gap-1">
                            {refused.map((mistake, index) => (
                                <li key={`${mistake.code}-${mistake.resource.id}-${mistake.path}-${index}`}>
                                    {mistake.message} {mistake.fix}{" "}
                                    <Anchor href={`${base}/${screenOf(mistake)}`} className="underline">
                                        {mistake.resource.id === "" ? "Open" : `Open ${mistake.resource.id}`}
                                    </Anchor>
                                </li>
                            ))}
                        </ul>
                    </div>
                </Alert>
            )}
            {versions.length === 0 ? (
                <p className="text-muted-foreground">No versions yet. Create one: the whole working state is checked and frozen.</p>
            ) : (
                <Paper>
                    <Table>
                        <thead>
                            <tr>
                                <th>Version</th>
                                <th>Content</th>
                                <th>Digest</th>
                                <th />
                            </tr>
                        </thead>
                        <tbody>
                            {versions.map((version) => (
                                <tr key={numberOf(version)}>
                                    <td className="font-medium">
                                        v{numberOf(version)} · {dateOf(version)}
                                    </td>
                                    <td className="text-muted-foreground">
                                        {version.facts} facts, {version.knowledgeBases} knowledge bases, {version.articles} articles,{" "}
                                        {version.procedures} procedures
                                    </td>
                                    <td>
                                        <code className="text-muted-foreground" title={version.sha256}>
                                            {digestOf(version.sha256)}
                                        </code>
                                    </td>
                                    <td className="text-right">
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            onClick={() => {
                                                onOpen(version);
                                            }}
                                        >
                                            Open
                                        </Button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </Table>
                </Paper>
            )}
            {shown !== null && (
                <Paper className="flex flex-col gap-2">
                    <div className="flex items-center gap-2">
                        <h2 className="flex-1 text-xl font-bold">Version {numberOf(shown.version)}</h2>
                        <Button variant="outline" size="sm" onClick={onClose}>
                            Close
                        </Button>
                    </div>
                    <p className="text-sm text-muted-foreground">sha256 {shown.version.sha256}</p>
                    <pre className="max-h-96 overflow-auto text-xs whitespace-pre-wrap">{shown.document}</pre>
                </Paper>
            )}
        </Page>
    );
}
