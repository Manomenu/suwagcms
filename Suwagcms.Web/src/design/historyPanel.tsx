import { Alert } from "@/design/alert";
import { Anchor } from "@/design/anchor";
import { Badge } from "@/design/badge";
import { changedLines } from "@/design/historyLines";
import { Paper } from "@/design/page";
import { Paginated, type Paging } from "@/design/pagination";

/** One recorded change as the list needs it. */
export interface HistoryLine {
    id: number | string;
    at: string;
    kind: string;
    resourceId: string;
    action: string;
    revision: null | number | string;
    changes: { field: string; old?: unknown; new?: unknown }[];
    batch: null | string;
}

const VARIANTS: Record<string, "success" | "primary" | "danger" | "neutral"> = {
    added: "success",
    edited: "primary",
    deleted: "danger",
};

function Fields({ line }: { line: HistoryLine }) {
    return (
        <ul className="flex flex-col gap-1">
            {line.changes.map((change) => {
                const { removed, added } = changedLines(change.old, change.new);
                return (
                    <li key={change.field} className="flex flex-col font-mono text-xs whitespace-pre-wrap">
                        <span className="font-semibold">{change.field}</span>
                        {removed.map((text, index) => (
                            <span key={`-${index.toString()}`} className="text-destructive">
                                − {text}
                            </span>
                        ))}
                        {added.map((text, index) => (
                            <span key={`+${index.toString()}`} className="text-success">
                                + {text}
                            </span>
                        ))}
                    </li>
                );
            })}
        </ul>
    );
}

function Entry({ line, hrefOf }: { line: HistoryLine; hrefOf: ((line: HistoryLine) => string) | undefined }) {
    return (
        <li className="flex flex-col gap-1 border-b border-border pb-2 last:border-b-0 last:pb-0">
            <div className="flex flex-wrap items-center gap-2 text-sm">
                <Badge variant={VARIANTS[line.action] ?? "neutral"}>{line.action}</Badge>
                {hrefOf !== undefined && (
                    <span>
                        {line.kind} <Anchor href={hrefOf(line)}>{line.resourceId}</Anchor>
                    </span>
                )}
                <span className="text-muted-foreground">{line.at.slice(0, 16).replace("T", " ")}</span>
                {line.revision !== null && <span className="text-muted-foreground">revision {String(line.revision)}</span>}
                {line.batch !== null && <span className="text-muted-foreground">batch {line.batch.slice(0, 8)}</span>}
            </div>
            {/* An add holds every field, which is the resource itself, so it stays folded. */}
            {line.action === "added" ? (
                <details className="text-sm">
                    <summary className="cursor-pointer text-muted-foreground">
                        {line.changes.length} {line.changes.length === 1 ? "field" : "fields"}
                    </summary>
                    <Fields line={line} />
                </details>
            ) : (
                <Fields line={line} />
            )}
        </li>
    );
}

/** The entries newest first; with no entries it says since when nothing has been recorded, so it cannot read as "never changed". */
export function HistoryList({
    entries,
    since,
    failure,
    paging,
    hrefOf,
}: {
    entries: HistoryLine[] | null;
    since: string | null;
    failure: string | null;
    paging: Paging;
    hrefOf?: (line: HistoryLine) => string;
}) {
    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (entries === null) return <p className="text-sm text-muted-foreground">Loading the history…</p>;
    if (entries.length === 0) {
        return (
            <p className="text-sm text-muted-foreground">No change has been recorded since {since?.slice(0, 10)}, when history started.</p>
        );
    }
    return (
        <Paginated paging={paging}>
            <ul className="flex flex-col gap-2">
                {entries.map((line) => (
                    <Entry key={String(line.id)} line={line} hrefOf={hrefOf} />
                ))}
            </ul>
        </Paginated>
    );
}

/** The History panel of one resource. */
export function HistoryPanel(props: { entries: HistoryLine[] | null; since: string | null; failure: string | null; paging: Paging }) {
    return (
        <Paper aria-label="History" role="region" className="flex flex-col gap-2 wrap-anywhere">
            <h3 className="text-sm font-semibold">History</h3>
            <HistoryList {...props} />
        </Paper>
    );
}
