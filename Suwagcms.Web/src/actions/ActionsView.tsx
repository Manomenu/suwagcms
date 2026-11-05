import type { ReactNode } from "react";

import { AvailabilityBadge } from "@/design/availabilityPanel";
import { Badge } from "@/design/badge";
import { Button } from "@/design/button";
import { Page, Paper } from "@/design/page";
import { Table } from "@/design/table";

import { isCode, type ActionView } from "./api";

/** The Actions screen: the heading, one list of the bot's actions, HTTP and code, and whatever is open. */
export function ActionsView({
    bot,
    actions,
    form,
    isOff,
    onAdd,
    onOpen,
}: {
    bot: string;
    actions: ActionView[];
    form: ReactNode;
    isOff?: (id: string) => boolean;
    onAdd: () => void;
    onOpen: (action: ActionView) => void;
}) {
    return (
        <Page title={`Actions of ${bot}`} actions={<Button onClick={onAdd}>Add an action</Button>}>
            {form}
            {actions.length === 0 ? (
                <p className="text-muted-foreground">
                    No actions yet. Add one: an action calls another system, and a step of a procedure maps what it takes and gives to
                    facts.
                </p>
            ) : (
                <Paper>
                    <Table>
                        <thead>
                            <tr>
                                <th>Id</th>
                                <th>Description</th>
                                <th>Kind</th>
                                <th>Availability</th>
                                <th>Used by</th>
                                <th />
                            </tr>
                        </thead>
                        <tbody>
                            {actions.map((action) => (
                                <tr key={action.id}>
                                    <td className="font-medium">{action.id}</td>
                                    <td>{action.description}</td>
                                    <td>
                                        <div className="flex flex-wrap items-center gap-2">
                                            <Badge variant={isCode(action) ? "neutral" : "primary"}>
                                                {isCode(action) ? "code" : "http"}
                                            </Badge>
                                            {action.alsoDeclared && <Badge variant="danger">the bot also declares this id</Badge>}
                                            {action.noLongerDeclared && <Badge variant="danger">no longer declared</Badge>}
                                        </div>
                                    </td>
                                    <td>
                                        <AvailabilityBadge off={isOff?.(action.id) ?? false} />
                                    </td>
                                    <td className="text-muted-foreground">
                                        {action.usedBy.length > 0 && `Used by: ${action.usedBy.join(", ")}`}
                                    </td>
                                    <td className="text-right">
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            onClick={() => {
                                                onOpen(action);
                                            }}
                                        >
                                            Open
                                        </Button>
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
