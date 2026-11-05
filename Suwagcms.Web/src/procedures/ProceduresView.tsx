import type { ReactNode } from "react";

import type { Mistake } from "@/api/client";
import { Anchor } from "@/design/anchor";
import { AvailabilityBadge } from "@/design/availabilityPanel";
import { Badge } from "@/design/badge";
import { Page, Paper } from "@/design/page";
import { Table } from "@/design/table";

import { mistakesOf, type ProcedureList } from "./api";
import { NewProcedureForm } from "./NewProcedureForm";

function Readiness({ count }: { count: number }) {
    return count === 0 ? (
        <Badge variant="success">ready</Badge>
    ) : (
        <Badge variant="warning">
            {count} {count === 1 ? "mistake" : "mistakes"} before a version
        </Badge>
    );
}

/** The Procedures screen: each procedure with its description and how many mistakes still stop a version. Each opens in the creator, and a new one starts there. */
export function ProceduresView({
    bot,
    list,
    isOff,
    notice,
}: {
    bot: string;
    list: ProcedureList;
    isOff?: (id: string) => boolean;
    notice?: ReactNode;
}) {
    const botMistakes: Mistake[] = list.botNotReady;
    return (
        <Page title={`Procedures of ${bot}`}>
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
            <NewProcedureForm bot={bot} />
            {list.procedures.length === 0 ? (
                <p className="text-muted-foreground">
                    No procedures yet. Create one above, or write it in a YAML file and add it with{" "}
                    <code>suwagcms procedures add --file</code>.
                </p>
            ) : (
                <Paper>
                    <Table>
                        <thead>
                            <tr>
                                <th>Procedure</th>
                                <th>Description</th>
                                <th>Readiness</th>
                                <th>Availability</th>
                            </tr>
                        </thead>
                        <tbody>
                            {list.procedures.map((procedure) => (
                                <tr key={procedure.id}>
                                    <td>
                                        <Anchor href={`~/bots/${encodeURIComponent(bot)}/procedures/${encodeURIComponent(procedure.id)}`}>
                                            {procedure.id}
                                        </Anchor>
                                    </td>
                                    <td className="text-muted-foreground">{procedure.description}</td>
                                    <td>
                                        <Readiness count={mistakesOf(procedure)} />
                                    </td>
                                    <td>
                                        <AvailabilityBadge off={isOff?.(procedure.id) ?? false} />
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </Table>
                </Paper>
            )}
        </Page>
    );
}
