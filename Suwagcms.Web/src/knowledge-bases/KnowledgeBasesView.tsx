import { ResourceList } from "@/design/resourceList";

import type { KnowledgeBase } from "./api";

/** The list of a bot's knowledge bases. */
export function KnowledgeBasesView({
    bot,
    bases,
    onAdd,
    onEdit,
}: {
    bot: string;
    bases: KnowledgeBase[];
    onAdd: () => void;
    onEdit: (base: KnowledgeBase) => void;
}) {
    return (
        <ResourceList
            title={`Knowledge bases of ${bot}`}
            addLabel="Add a knowledge base"
            onAdd={onAdd}
            rows={bases}
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
