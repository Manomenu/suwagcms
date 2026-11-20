import { usePagedRows } from "@/api/pagination";
import type { HistoryPanelProps } from "@/design/page";
import { ResourceList } from "@/design/resourceList";

import type { KnowledgeBase } from "./api";

export function KnowledgeBasesView({
    bot,
    history,
    bases,
    onAdd,
    onEdit,
}: {
    bot: string;
    history: HistoryPanelProps;
    bases: KnowledgeBase[];
    onAdd: () => void;
    onEdit: (base: KnowledgeBase) => void;
}) {
    const paged = usePagedRows(bases);
    return (
        <ResourceList
            history={history}
            title={`Knowledge bases of ${bot}`}
            addLabel="Add a knowledge base"
            onAdd={onAdd}
            rows={paged.rows}
            paging={paged.paging}
            idOf={(base) => base.id}
            onOpen={onEdit}
            columns={[
                { header: "Title", className: "font-medium", cell: (base) => base.title },
                { header: "Id", className: "text-muted-foreground", cell: (base) => base.id },
                { header: "Language", cell: (base) => base.language },
            ]}
            empty={
                <p className="text-muted-foreground">
                    No knowledge bases yet. Create the first one: it groups the articles of the bot, for people to find and work on them
                    apart.
                </p>
            }
        />
    );
}
