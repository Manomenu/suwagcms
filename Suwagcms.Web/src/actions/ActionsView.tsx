import type { ReactNode } from "react";

import { usePagedRows } from "@/api/pagination";
import { AvailabilityBadge } from "@/design/availabilityPanel";
import { Badge } from "@/design/badge";
import { ResourceList } from "@/design/resourceList";

import { isCode, type ActionView } from "./api";

/** One list of the bot's actions, HTTP and code. */
export function ActionsView({
    bot,
    actions,
    isOff,
    notice,
    onAdd,
    onOpen,
}: {
    bot: string;
    actions: ActionView[];
    isOff?: (id: string) => boolean;
    notice?: ReactNode;
    onAdd: () => void;
    onOpen: (action: ActionView) => void;
}) {
    const paged = usePagedRows(actions);
    return (
        <ResourceList
            title={`Actions of ${bot}`}
            addLabel="Add an action"
            onAdd={onAdd}
            above={notice}
            rows={paged.rows}
            paging={paged.paging}
            idOf={(action) => action.id}
            openLabel="Open"
            onOpen={onOpen}
            columns={[
                { header: "Id", className: "font-medium", cell: (action) => action.id },
                { header: "Description", cell: (action) => action.description },
                {
                    header: "Kind",
                    cell: (action) => (
                        <div className="flex flex-wrap items-center gap-2">
                            <Badge variant={isCode(action) ? "neutral" : "primary"}>{isCode(action) ? "code" : "http"}</Badge>
                            {action.alsoDeclared && <Badge variant="danger">the bot also declares this id</Badge>}
                            {action.noLongerDeclared && <Badge variant="danger">no longer declared</Badge>}
                        </div>
                    ),
                },
                { header: "Availability", cell: (action) => <AvailabilityBadge off={isOff?.(action.id) ?? false} /> },
                {
                    header: "Used by",
                    className: "text-muted-foreground",
                    cell: (action) => action.usedBy.length > 0 && `Used by: ${action.usedBy.join(", ")}`,
                },
            ]}
            empty={
                <p className="text-muted-foreground">
                    No actions yet. Add one: an action calls another system, and a step of a procedure maps what it takes and gives to
                    facts.
                </p>
            }
        />
    );
}
