import type { ReactNode } from "react";

import { Button } from "@/design/button";
import { Page, Paper } from "@/design/page";
import { Table } from "@/design/table";

import type { Fact } from "./api";
import { constraintOf } from "./factForm";

/** The Facts screen: the heading, the list with its actions, and whatever form is open. */
export function FactsView({
    bot,
    facts,
    form,
    onAdd,
    onEdit,
}: {
    bot: string;
    facts: Fact[];
    form: ReactNode;
    onAdd: () => void;
    onEdit: (fact: Fact) => void;
}) {
    return (
        <Page title={`Facts of ${bot}`} actions={<Button onClick={onAdd}>Add a fact</Button>}>
            {form}
            {facts.length === 0 ? (
                <p className="text-muted-foreground">
                    No facts yet. Add one: the model reads each fact's description to understand a message.
                </p>
            ) : (
                <Paper>
                    <Table>
                        <thead>
                            <tr>
                                <th>Name</th>
                                <th>Type</th>
                                <th>Description</th>
                                <th>Used by</th>
                                <th />
                            </tr>
                        </thead>
                        <tbody>
                            {facts.map((fact) => (
                                <tr key={fact.name}>
                                    <td className="font-medium">{fact.name}</td>
                                    <td className="text-muted-foreground">
                                        {constraintOf(fact)} · {fact.writableBy}
                                    </td>
                                    <td>{fact.description}</td>
                                    <td className="text-muted-foreground">
                                        {fact.usedBy.length > 0 && `Used by: ${fact.usedBy.join(", ")}`}
                                    </td>
                                    <td className="text-right">
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            onClick={() => {
                                                onEdit(fact);
                                            }}
                                        >
                                            Edit
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
