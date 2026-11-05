import { useState } from "react";

import type { Mistake } from "@/api/client";
import { Alert } from "@/design/alert";
import { Anchor } from "@/design/anchor";
import { Button } from "@/design/button";
import { Page, Paper } from "@/design/page";
import { Select } from "@/design/select";
import { Table } from "@/design/table";

import {
    numberOf,
    type BotStatus,
    type PushPlan,
    type PushResult,
    type VersionComparison,
    type VersionDetail,
    type VersionSummary,
} from "./api";
import { ComparisonDetails, comparisonLine, ComparisonPanel } from "./ComparisonPanel";
import { otherVersions, statusView, whenOf } from "./pushStatus";

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

/** The rollback's choice beside the main button: a version in a select, and the button that asks before pushing it. */
function OtherVersion({ numbers, pushing, onPlan }: { numbers: number[]; pushing: boolean; onPlan: (number: number) => void }) {
    const [picked, setPicked] = useState<number | null>(null);
    const chosen = picked !== null && numbers.includes(picked) ? picked : numbers[0];
    if (chosen === undefined) return null;
    return (
        <>
            <label className="flex items-center gap-2 text-sm font-medium">
                other version:
                <Select
                    value={chosen}
                    onChange={(event) => {
                        setPicked(Number(event.target.value));
                    }}
                >
                    {numbers.map((number) => (
                        <option key={number} value={number}>
                            v{number}
                        </option>
                    ))}
                </Select>
            </label>
            <Button
                variant="outline"
                disabled={pushing}
                onClick={() => {
                    onPlan(chosen);
                }}
            >
                Push v{chosen}
            </Button>
        </>
    );
}

/** The one question before a rollback: where the bot goes, what stays, and what going there changes. */
function PushQuestion({ plan, onPush, onCancel }: { plan: PushPlan; onPush: (number: number) => void; onCancel: () => void }) {
    const version = Number(plan.version);
    return (
        <div className="flex flex-col gap-2 rounded-md border border-border p-3">
            <p className="text-sm">
                The bot goes {plan.botHas === null ? "" : `from v${Number(plan.botHas)} `}to v{version}. The working state and the latest
                version stay as they are. Conversations in progress finish on their version.
            </p>
            {plan.problem !== null && <p className="text-sm text-destructive">{plan.problem}</p>}
            {plan.changes !== null && (
                <>
                    <p className="text-sm font-medium">What goes back: {comparisonLine(plan.changes)}</p>
                    <ComparisonDetails comparison={plan.changes} />
                </>
            )}
            <div className="flex items-center gap-2">
                <Button
                    onClick={() => {
                        onPush(version);
                    }}
                >
                    Push v{version}
                </Button>
                <Button variant="outline" onClick={onCancel}>
                    Cancel
                </Button>
            </div>
        </div>
    );
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
    comparison,
    plan,
    onCompare,
    onCloseComparison,
    onPlan,
    onCancelPlan,
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
    comparison: VersionComparison | null;
    plan: PushPlan | null;
    onCompare: (from: number, to: number) => void;
    onCloseComparison: () => void;
    onPlan: (number: number) => void;
    onCancelPlan: () => void;
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
                    <div className="flex flex-wrap items-center gap-3">
                        <p className={`flex-1 font-medium ${bots.problem ? "text-destructive" : ""}`}>{bots.headline}</p>
                        {bots.compare && (
                            <Button
                                variant="outline"
                                onClick={() => {
                                    if (bots.compare) onCompare(bots.compare.from, bots.compare.to);
                                }}
                            >
                                Compare v{bots.compare.from} with v{bots.compare.to}
                            </Button>
                        )}
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
                        <OtherVersion numbers={otherVersions(versions.map(numberOf), status)} pushing={pushing} onPlan={onPlan} />
                    </div>
                    {plan !== null && <PushQuestion plan={plan} onPush={onPush} onCancel={onCancelPlan} />}
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
                            {versions.map((version, index) => (
                                <tr key={numberOf(version)}>
                                    <td className="font-medium">
                                        v{numberOf(version)} · {dateOf(version)}
                                    </td>
                                    <td className="text-muted-foreground">
                                        {version.facts} facts, {version.knowledgeBases} knowledge bases, {version.articles} articles,{" "}
                                        {version.procedures} procedures, {version.actions} actions
                                    </td>
                                    <td>
                                        <code className="text-muted-foreground" title={version.sha256}>
                                            {digestOf(version.sha256)}
                                        </code>
                                    </td>
                                    <td>
                                        <div className="flex items-center justify-end gap-2">
                                            {versions[index + 1] === undefined ? (
                                                <span className="text-sm text-muted-foreground">Nothing before it</span>
                                            ) : (
                                                <Button
                                                    variant="outline"
                                                    size="sm"
                                                    onClick={() => {
                                                        onCompare(numberOf(versions[index + 1] ?? version), numberOf(version));
                                                    }}
                                                >
                                                    Changes
                                                </Button>
                                            )}
                                            <Button
                                                variant="outline"
                                                size="sm"
                                                onClick={() => {
                                                    onOpen(version);
                                                }}
                                            >
                                                Open
                                            </Button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </Table>
                </Paper>
            )}
            {comparison !== null && (
                <ComparisonPanel comparison={comparison} versions={versions} onCompare={onCompare} onClose={onCloseComparison} />
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
