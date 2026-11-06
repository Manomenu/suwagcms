import type * as React from "react";

import { Alert } from "@/design/alert";
import { Anchor } from "@/design/anchor";
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
    onFollow,
}: {
    title: string;
    entries: UsageEntry[];
    empty: string;
    hrefOf: (entry: UsageEntry) => string;
    onFollow?: (event: React.MouseEvent) => void;
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
                            <Anchor href={hrefOf(entry)} {...(onFollow === undefined ? {} : { onClick: onFollow })}>
                                {entry.id}
                            </Anchor>{" "}
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

/** The "Used by" and "Uses" panels of one resource; the list is loading when `usage` is null. `onFollow` may stop a link, as unsaved changes do. */
export function UsagePanel({
    usage,
    failure,
    hrefOf,
    onFollow,
}: {
    usage: { usedBy: UsageEntry[]; uses: UsageEntry[] } | null;
    failure: string | null;
    hrefOf: (entry: UsageEntry) => string;
    onFollow?: (event: React.MouseEvent) => void;
}) {
    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (usage === null) return <p className="text-sm text-muted-foreground">Loading what uses this…</p>;
    const follow = onFollow === undefined ? {} : { onFollow };
    return (
        <div className="flex flex-wrap gap-3">
            <Card title="Used by" entries={usage.usedBy} empty="Nothing uses this." hrefOf={hrefOf} {...follow} />
            <Card title="Uses" entries={usage.uses} empty="This uses nothing." hrefOf={hrefOf} {...follow} />
        </div>
    );
}
