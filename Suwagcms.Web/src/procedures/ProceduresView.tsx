import type { ReactNode } from "react";

import type { Mistake } from "@/api/client";
import { editHref } from "@/api/resourceAddress";
import { Anchor } from "@/design/anchor";
import { AvailabilityBadge } from "@/design/availabilityPanel";
import { Badge } from "@/design/badge";
import { Paper } from "@/design/page";
import { ResourceList } from "@/design/resourceList";

import { mistakesOf, type ProcedureList } from "./api";

function Readiness({ count }: { count: number }) {
    return count === 0 ? (
        <Badge variant="success">ready</Badge>
    ) : (
        <Badge variant="warning">
            {count} {count === 1 ? "mistake" : "mistakes"} before a version
        </Badge>
    );
}

/** The list of a bot's procedures, each with its description and how many mistakes still stop a version. Each opens in the editor. */
export function ProceduresView({
    bot,
    list,
    isOff,
    notice,
    onAdd,
    onOpen,
}: {
    bot: string;
    list: ProcedureList;
    isOff?: (id: string) => boolean;
    notice?: ReactNode;
    onAdd: () => void;
    onOpen: (id: string) => void;
}) {
    const botMistakes: Mistake[] = list.botNotReady;
    return (
        <ResourceList
            title={`Procedures of ${bot}`}
            addLabel="Add a procedure"
            onAdd={onAdd}
            above={
                <>
                    {notice}
                    {botMistakes.length > 0 && (
                        <Paper className="border-warning">
                            <ul className="flex flex-col gap-1 text-sm">
                                {botMistakes.map((mistake) => (
                                    <li key={mistake.code}>
                                        {mistake.message} {mistake.fix}
                                    </li>
                                ))}
                            </ul>
                        </Paper>
                    )}
                </>
            }
            rows={list.procedures}
            idOf={(procedure) => procedure.id}
            onOpen={(procedure) => {
                onOpen(procedure.id);
            }}
            columns={[
                {
                    header: "Procedure",
                    cell: (procedure) => <Anchor href={`~${editHref(bot, "procedures", procedure.id)}`}>{procedure.id}</Anchor>,
                },
                { header: "Description", className: "text-muted-foreground", cell: (procedure) => procedure.description },
                { header: "Readiness", cell: (procedure) => <Readiness count={mistakesOf(procedure)} /> },
                { header: "Availability", cell: (procedure) => <AvailabilityBadge off={isOff?.(procedure.id) ?? false} /> },
            ]}
            empty={
                <p className="text-muted-foreground">
                    No procedures yet. Add one, or write it in a YAML file and add it with <code>suwagcms procedures add --file</code>.
                </p>
            }
        />
    );
}
