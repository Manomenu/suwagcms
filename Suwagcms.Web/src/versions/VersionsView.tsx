import { useState } from "react";

import type { Mistake } from "@/api/client";
import { usePagedRows } from "@/api/pagination";
import { Alert } from "@/design/alert";
import { Anchor } from "@/design/anchor";
import { Button } from "@/design/button";
import { Page, Paper, type HistoryPanelProps } from "@/design/page";
import { Paginated } from "@/design/pagination";
import { Select } from "@/design/select";
import { Table } from "@/design/table";

import { contentOf, dateOf, numberOf, type BotStatus, type PushPlan, type PushResult, type VersionSummary } from "./api";
import { comparisonLine, ComparisonRows } from "./comparison";
import { otherVersions, statusView, whenOf } from "./pushStatus";

const SCREENS: Record<Mistake["resource"]["kind"], string> = {
    GlobalFact: "facts",
    KnowledgeBase: "knowledge-bases",
    Article: "articles",
    Procedure: "procedures",
    Action: "procedures",
    Persona: "persona",
    Definition: "procedures",
};

function screenOf(mistake: Mistake): string {
    return SCREENS[mistake.resource.kind];
}

function digestOf(sha256: string): string {
    return `${sha256.slice(0, 4)}…${sha256.slice(-4)}`;
}

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
                    <ComparisonRows comparison={plan.changes} />
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

export function VersionsView({
    bot,
    versions,
    history,
    status,
    pushFailure,
    pushing,
    refused,
    plan,
    onCompare,
    onPlan,
    onCancelPlan,
    onPush,
    onCreate,
    onOpen,
}: {
    bot: string;
    versions: VersionSummary[];
    history: HistoryPanelProps;
    status: BotStatus | null;
    pushFailure: PushResult | null;
    pushing: boolean;
    refused: Mistake[] | null;
    plan: PushPlan | null;
    onCompare: (from: number, to: number) => void;
    onPlan: (number: number) => void;
    onCancelPlan: () => void;
    onPush: (number: number) => void;
    onCreate: () => void;
    onOpen: (version: VersionSummary) => void;
}) {
    const base = `~/bots/${encodeURIComponent(bot)}`;
    const bots = status === null ? null : statusView(status);
    const paged = usePagedRows(versions);
    return (
        <Page
            title={`Versions of ${bot}`}
            history={history}
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
                <Paginated paging={paged.paging}>
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
                                {paged.rows.map((version) => (
                                    <tr key={numberOf(version)}>
                                        <td className="font-medium">
                                            v{numberOf(version)} · {dateOf(version)}
                                        </td>
                                        <td className="text-muted-foreground">{contentOf(version)}</td>
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
                </Paginated>
            )}
        </Page>
    );
}
