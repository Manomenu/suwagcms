import { Badge } from "@/design/badge";
import { Button } from "@/design/button";
import { Paper } from "@/design/page";
import { Select } from "@/design/select";

import { numberOf, type FieldDifference, type ResourceDifference, type VersionComparison, type VersionSummary } from "./api";

const KINDS: Record<ResourceDifference["kind"], string> = {
    GlobalFact: "fact",
    KnowledgeBase: "knowledge base",
    Article: "article",
    Procedure: "procedure",
    Action: "action",
    Persona: "persona",
    Definition: "definition",
};

const VARIANTS = { Added: "success", Removed: "danger", Changed: "primary" } as const;

const LINE_MARKS = { Removed: "− ", Added: "+ ", Kept: "  ", Folded: "… " } as const;

const LINE_COLORS = { Removed: "text-destructive", Added: "text-success", Kept: "", Folded: "text-muted-foreground" } as const;

function Field({ field }: { field: FieldDifference }) {
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

function Resource({ resource }: { resource: ResourceDifference }) {
    return (
        <li className="flex flex-col gap-1 border-t border-border pt-2">
            <div className="flex items-center gap-2">
                <Badge variant={VARIANTS[resource.difference]}>{resource.difference.toLowerCase()}</Badge>
                <span className="font-medium">{resource.id}</span>
                <span className="text-sm text-muted-foreground">{KINDS[resource.kind]}</span>
            </div>
            {resource.fields.map((field) => (
                <Field key={field.field} field={field} />
            ))}
            {resource.parts.map((part) => (
                <div key={`${part.part}-${part.name}`} className="flex flex-col gap-1 pl-4">
                    <p className="text-sm font-medium">
                        {part.part} '{part.name}' {part.difference.toLowerCase()}
                    </p>
                    {part.fields.map((field) => (
                        <Field key={field.field} field={field} />
                    ))}
                </div>
            ))}
        </li>
    );
}

/** One line of counts for a comparison. */
export function comparisonLine(comparison: VersionComparison): string {
    return comparison.resources.length === 0 && comparison.definition.length === 0
        ? "no differences"
        : `${Number(comparison.added)} added · ${Number(comparison.removed)} removed · ${Number(comparison.changed)} changed`;
}

/** Each field and resource a comparison finds changed. */
export function ComparisonDetails({ comparison }: { comparison: VersionComparison }) {
    return (
        <>
            {comparison.definition.map((field) => (
                <Field key={field.field} field={field} />
            ))}
            {comparison.resources.length > 0 && (
                <ul className="flex flex-col gap-2">
                    {comparison.resources.map((resource) => (
                        <Resource key={`${resource.kind}-${resource.id}`} resource={resource} />
                    ))}
                </ul>
            )}
        </>
    );
}

/** What going from one version to another changes; the select compares the same version with any other. */
export function ComparisonPanel({
    comparison,
    versions,
    onCompare,
    onClose,
}: {
    comparison: VersionComparison;
    versions: VersionSummary[];
    onCompare: (from: number, to: number) => void;
    onClose: () => void;
}) {
    const from = Number(comparison.from);
    const to = Number(comparison.to);
    return (
        <Paper className="flex flex-col gap-3">
            <div className="flex items-center gap-2">
                <label className="flex items-center gap-2 text-sm font-medium">
                    From
                    <Select
                        value={from}
                        onChange={(event) => {
                            onCompare(Number(event.target.value), to);
                        }}
                    >
                        {versions
                            .filter((version) => numberOf(version) !== to || numberOf(version) === from)
                            .map((version) => (
                                <option key={numberOf(version)} value={numberOf(version)}>
                                    v{numberOf(version)}
                                </option>
                            ))}
                    </Select>
                </label>
                <span className="text-sm font-medium">to v{to}</span>
                <span className="flex-1 text-sm text-muted-foreground">{comparisonLine(comparison)}</span>
                <Button variant="outline" size="sm" onClick={onClose}>
                    Close
                </Button>
            </div>
            <ComparisonDetails comparison={comparison} />
        </Paper>
    );
}
