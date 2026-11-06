import type { ReactNode } from "react";

import { Alert } from "@/design/alert";
import { Anchor } from "@/design/anchor";
import { Badge } from "@/design/badge";
import { Paginated, type Paging } from "@/design/pagination";

export interface WrittenField {
    field: string;
    old: null | string;
    new: null | string;
    lines: null | { change: "Kept" | "Removed" | "Added" | "Folded"; text: string }[];
}

export interface WrittenPart {
    part: string;
    name: string;
    difference: "Added" | "Removed" | "Changed";
    fields: WrittenField[];
}

/** One recorded change as the list needs it. */
export interface ChangeLine {
    id: number | string;
    at: string;
    kind: string;
    resourceId: string;
    action: string;
    revision: null | number | string;
    changes: unknown[];
    batch: null | string;
    written?: null | { fields: WrittenField[]; parts: WrittenPart[] };
}

// Why: an entry says what happened and a comparison the net result, so each keeps its own word, and the same colour means the same thing in both.
const VARIANTS: Record<string, "success" | "primary" | "danger" | "neutral"> = {
    added: "success",
    edited: "primary",
    changed: "primary",
    created: "primary",
    deleted: "danger",
    removed: "danger",
};

const LINE_MARKS = { Removed: "− ", Added: "+ ", Kept: "  ", Folded: "… " } as const;

const LINE_COLORS = { Removed: "text-destructive", Added: "text-success", Kept: "", Folded: "text-muted-foreground" } as const;

function Field({ field }: { field: WrittenField }) {
    if (field.lines === null) {
        return (
            <p className="text-sm">
                <span className="text-muted-foreground">{field.field}: </span>
                <span className="text-destructive">{field.old ?? "(none)"}</span> →{" "}
                <span className="text-success">{field.new ?? "(none)"}</span>
            </p>
        );
    }
    return (
        <div className="text-sm">
            <p className="text-muted-foreground">{field.field}</p>
            <pre className="overflow-auto text-xs whitespace-pre-wrap">
                {field.lines.map((line, index) => (
                    <div key={index} className={LINE_COLORS[line.change]}>
                        {LINE_MARKS[line.change]}
                        {line.text}
                    </div>
                ))}
            </pre>
        </div>
    );
}

function Part({ part }: { part: WrittenPart }) {
    const line = `${part.part} '${part.name}' ${part.difference.toLowerCase()}`;
    if (part.fields.length === 0) return <p className="text-sm font-medium">{line}</p>;
    return (
        <details className="text-sm">
            <summary className="cursor-pointer font-medium">{line}</summary>
            <div className="flex flex-col gap-1 pl-4">
                {part.fields.map((field) => (
                    <Field key={field.field} field={field} />
                ))}
            </div>
        </details>
    );
}

/** The one row of a change: the action, the resource when the page lists several, what else is known of it, and the change below. */
export function ChangeRow({
    action,
    resource,
    details = [],
    fields,
    parts,
    foldedAs,
}: {
    action: string;
    resource?: ReactNode;
    details?: string[];
    fields: WrittenField[];
    parts: WrittenPart[];
    foldedAs?: string;
}) {
    const change = (
        <>
            {fields.map((field) => (
                <Field key={field.field} field={field} />
            ))}
            {parts.map((part) => (
                <Part key={`${part.part}-${part.name}`} part={part} />
            ))}
        </>
    );
    return (
        <li className="flex flex-col gap-1 border-b border-border pb-2 last:border-b-0 last:pb-0">
            <div className="flex flex-wrap items-center gap-2 text-sm">
                <Badge variant={VARIANTS[action] ?? "neutral"}>{action}</Badge>
                {resource !== undefined && <span>{resource}</span>}
                {details.map((detail) => (
                    <span key={detail} className="text-muted-foreground">
                        {detail}
                    </span>
                ))}
            </div>
            {foldedAs === undefined ? (
                change
            ) : (
                <details className="text-sm">
                    <summary className="cursor-pointer text-muted-foreground">{foldedAs}</summary>
                    {change}
                </details>
            )}
        </li>
    );
}

function detailsOf(line: ChangeLine): string[] {
    return [
        line.at.slice(0, 16).replace("T", " "),
        ...(line.revision === null ? [] : [`revision ${String(line.revision)}`]),
        ...(line.batch === null ? [] : [`batch ${line.batch.slice(0, 8)}`]),
    ];
}

function Entry({ line, hrefOf }: { line: ChangeLine; hrefOf: ((line: ChangeLine) => string) | undefined }) {
    return (
        <ChangeRow
            action={line.action}
            details={detailsOf(line)}
            fields={line.written?.fields ?? []}
            parts={line.written?.parts ?? []}
            {...(hrefOf === undefined
                ? {}
                : {
                      resource: (
                          <>
                              {line.kind} <Anchor href={hrefOf(line)}>{line.resourceId}</Anchor>
                          </>
                      ),
                  })}
            // Why: an add holds every field, which is the resource itself, so it stays folded.
            {...(line.action === "added" ? { foldedAs: `${line.changes.length} ${line.changes.length === 1 ? "field" : "fields"}` } : {})}
        />
    );
}

/**
 * The entries newest first, under whatever name the page gives them. With no entries it says since when nothing has been recorded,
 * or what `empty` says, so it cannot read as "never changed".
 */
export function ChangeList({
    entries,
    since,
    failure,
    paging,
    hrefOf,
    empty,
}: {
    entries: ChangeLine[] | null;
    since: string | null;
    failure: string | null;
    paging: Paging;
    hrefOf?: (line: ChangeLine) => string;
    empty?: string;
}) {
    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (entries === null) return <p className="text-sm text-muted-foreground">Loading…</p>;
    if (entries.length === 0) {
        return (
            <p className="text-sm text-muted-foreground">{empty ?? `No change has been recorded since ${since?.slice(0, 10) ?? ""}.`}</p>
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
