import type { ReactNode } from "react";

import { Button } from "@/design/button";
import { Page, Paper } from "@/design/page";
import { Table } from "@/design/table";

import type { KnowledgeBase } from "./api";

/** The Knowledge bases screen: the heading, the list with its actions, and whatever form is open. */
export function KnowledgeBasesView({
    bot,
    bases,
    form,
    onAdd,
    onEdit,
    onDelete,
}: {
    bot: string;
    bases: KnowledgeBase[];
    form: ReactNode;
    onAdd: () => void;
    onEdit: (base: KnowledgeBase) => void;
    onDelete: (base: KnowledgeBase) => void;
}) {
    return (
        <Page title={`Knowledge bases of ${bot}`} actions={<Button onClick={onAdd}>Add a knowledge base</Button>}>
            {form}
            {bases.length === 0 ? (
                <p className="text-muted-foreground">
                    No knowledge bases yet. Create the first one: it groups the articles of the bot, for people to find and work on them
                    apart.
                </p>
            ) : (
                <Paper>
                    <Table>
                        <thead>
                            <tr>
                                <th>Title</th>
                                <th>Id</th>
                                <th>Language</th>
                                <th />
                            </tr>
                        </thead>
                        <tbody>
                            {bases.map((base) => (
                                <tr key={base.id}>
                                    <td className="font-medium">{base.title}</td>
                                    <td className="text-muted-foreground">{base.id}</td>
                                    <td>{base.language}</td>
                                    <td>
                                        <div className="flex justify-end gap-2">
                                            <Button
                                                variant="outline"
                                                size="sm"
                                                onClick={() => {
                                                    onEdit(base);
                                                }}
                                            >
                                                Edit
                                            </Button>
                                            <Button
                                                variant="destructive"
                                                size="sm"
                                                onClick={() => {
                                                    onDelete(base);
                                                }}
                                            >
                                                Delete
                                            </Button>
                                        </div>
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
