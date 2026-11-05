import { Alert } from "@/design/alert";
import { Anchor } from "@/design/anchor";
import { Button } from "@/design/button";
import { Paper } from "@/design/page";

/** One reference as the panel needs it: the resource on the other end and the field of the referrer that holds it. */
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

/**
 * The "Used by" and "Uses" panels of one resource, with its Delete button when `onDelete` is given. A resource something
 * refers to cannot be deleted, so the button is disabled with the reason beside it; the list is loading when `usage` is null.
 */
export function UsagePanel({
    usage,
    failure,
    hrefOf,
    onDelete,
}: {
    usage: { usedBy: UsageEntry[]; uses: UsageEntry[] } | null;
    failure: string | null;
    hrefOf: (entry: UsageEntry) => string;
    onDelete?: () => void;
}) {
    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (usage === null) return <p className="text-sm text-muted-foreground">Loading what uses this…</p>;
    const count = usage.usedBy.length;
    return (
        <div className="flex flex-col gap-3">
            {onDelete !== undefined && (
                <div className="flex items-center gap-3">
                    <Button type="button" variant="destructive" disabled={count > 0} onClick={onDelete}>
                        Delete
                    </Button>
                    {count > 0 && (
                        <span className="text-sm text-muted-foreground">
                            Used in {count} {count === 1 ? "place" : "places"}. Remove {count === 1 ? "that reference" : "those references"}{" "}
                            first.
                        </span>
                    )}
                </div>
            )}
            <div className="flex flex-wrap gap-3">
                <Card title="Used by" entries={usage.usedBy} empty="Nothing uses this." hrefOf={hrefOf} />
                <Card title="Uses" entries={usage.uses} empty="This uses nothing." hrefOf={hrefOf} />
            </div>
        </div>
    );
}
