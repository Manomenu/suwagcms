import { usePagedRows } from "@/api/pagination";
import type { HistoryPanelProps } from "@/design/page";
import { ResourceList } from "@/design/resourceList";

import type { Fact } from "./api";
import { constraintOf } from "./factForm";

/** The list of a bot's facts. A fact's name is its id. */
export function FactsView({
    bot,
    facts,
    history,
    onAdd,
    onEdit,
}: {
    bot: string;
    facts: Fact[];
    history: HistoryPanelProps;
    onAdd: () => void;
    onEdit: (fact: Fact) => void;
}) {
    const paged = usePagedRows(facts);
    return (
        <ResourceList
            history={history}
            title={`Facts of ${bot}`}
            addLabel="Add a fact"
            onAdd={onAdd}
            rows={paged.rows}
            paging={paged.paging}
            idOf={(fact) => fact.name}
            onOpen={onEdit}
            columns={[
                { header: "Name", className: "font-medium", cell: (fact) => fact.name },
                { header: "Type", className: "text-muted-foreground", cell: (fact) => `${constraintOf(fact)} · ${fact.writableBy}` },
                { header: "Description", cell: (fact) => fact.description },
                {
                    header: "Used by",
                    className: "text-muted-foreground",
                    cell: (fact) => fact.usedBy.length > 0 && `Used by: ${fact.usedBy.join(", ")}`,
                },
            ]}
            empty={
                <p className="text-muted-foreground">
                    No facts yet. Add one: the model reads each fact's description to understand a message.
                </p>
            }
        />
    );
}
