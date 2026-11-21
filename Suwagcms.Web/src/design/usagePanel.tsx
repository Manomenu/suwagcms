import { Alert, Anchor, Paper, Spinner } from "@maniumek/suwfrontkit";

export interface UsageEntry {
    kind: string;
    id: string;
    field: string;
}

function Card({
    title,
    entries,
    empty,
    hrefOf,
}: {
    title: string;
    entries: UsageEntry[];
    empty: string;
    hrefOf: (entry: UsageEntry) => string;
}) {
    return (
        <Paper aria-label={title} role="region" className="flex min-w-0 flex-1 basis-64 flex-col gap-1 text-sm wrap-anywhere">
            <h3 className="font-semibold">{title}</h3>
            {entries.length === 0 ? (
                <p className="text-muted-foreground">{empty}</p>
            ) : (
                <ul className="flex flex-col gap-1">
                    {entries.map((entry) => (
                        <li key={`${entry.kind}:${entry.id}:${entry.field}`}>
                            <Anchor href={hrefOf(entry)}>{entry.id}</Anchor>{" "}
                            <span className="text-muted-foreground">
                                {entry.kind} · {entry.field}
                            </span>
                        </li>
                    ))}
                </ul>
            )}
        </Paper>
    );
}

export function UsagePanel({
    usage,
    failure,
    hrefOf,
    showUses = true,
}: {
    usage: { usedBy: UsageEntry[]; uses: UsageEntry[] } | null;
    failure: string | null;
    hrefOf: (entry: UsageEntry) => string;
    showUses?: boolean;
}) {
    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (usage === null) return <Spinner label="Loading what uses this…" size="sm" className="text-sm" />;
    return (
        <div className="flex flex-wrap gap-3">
            <Card title="Used by" entries={usage.usedBy} empty="Nothing uses this." hrefOf={hrefOf} />
            {showUses && <Card title="Uses" entries={usage.uses} empty="This uses nothing." hrefOf={hrefOf} />}
        </div>
    );
}
