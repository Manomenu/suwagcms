import { ChangeRow } from "@/design/changeList";

import type { ResourceDifference, VersionComparison } from "./api";

const KINDS: Record<ResourceDifference["kind"], string> = {
    GlobalFact: "fact",
    KnowledgeBase: "knowledge base",
    Article: "article",
    Procedure: "procedure",
    Action: "action",
    Persona: "persona",
    Definition: "definition",
};

export function comparisonLine(comparison: VersionComparison): string {
    return comparison.resources.length === 0 && comparison.definition.length === 0
        ? "no differences"
        : `${Number(comparison.added)} added · ${Number(comparison.removed)} removed · ${Number(comparison.changed)} changed`;
}

export function ComparisonRows({ comparison }: { comparison: VersionComparison }) {
    if (comparison.definition.length === 0 && comparison.resources.length === 0) return null;
    return (
        <ul className="flex flex-col gap-2">
            {comparison.definition.length > 0 && (
                <ChangeRow action="changed" resource={KINDS.Definition} fields={comparison.definition} parts={[]} />
            )}
            {comparison.resources.map((resource) => (
                <ChangeRow
                    key={`${resource.kind}-${resource.id}`}
                    action={resource.difference.toLowerCase()}
                    resource={
                        <>
                            {KINDS[resource.kind]} <span className="font-medium">{resource.id}</span>
                        </>
                    }
                    fields={resource.fields}
                    parts={resource.parts}
                />
            ))}
        </ul>
    );
}
