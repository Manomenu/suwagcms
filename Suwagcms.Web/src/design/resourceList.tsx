import type { ReactNode } from "react";

import { Button } from "@/design/button";
import { Page, Paper } from "@/design/page";
import { Table } from "@/design/table";

export interface Column<Row> {
    header: string;
    className?: string;
    cell: (row: Row) => ReactNode;
}

/**
 * The list screen of a resource: its heading, the button that adds one, whatever the kind shows above the list, and one table whose
 * rows only open a resource. A kind passes its own columns; what is done to a resource is done on its edit screen.
 */
export function ResourceList<Row>({
    title,
    addLabel,
    addDisabled = false,
    onAdd,
    above,
    rows,
    columns,
    idOf,
    openLabel = "Edit",
    onOpen,
    empty,
}: {
    title: string;
    addLabel: string;
    addDisabled?: boolean;
    onAdd: () => void;
    above?: ReactNode;
    rows: Row[];
    columns: Column<Row>[];
    idOf: (row: Row) => string;
    openLabel?: string;
    onOpen: (row: Row) => void;
    empty: ReactNode;
}) {
    return (
        <Page
            title={title}
            actions={
                <Button onClick={onAdd} disabled={addDisabled}>
                    {addLabel}
                </Button>
            }
        >
            {above}
            {rows.length === 0 ? (
                empty
            ) : (
                <Paper>
                    <Table>
                        <thead>
                            <tr>
                                {columns.map((column) => (
                                    <th key={column.header}>{column.header}</th>
                                ))}
                                <th />
                            </tr>
                        </thead>
                        <tbody>
                            {rows.map((row) => (
                                <tr key={idOf(row)}>
                                    {columns.map((column) => (
                                        <td key={column.header} className={column.className}>
                                            {column.cell(row)}
                                        </td>
                                    ))}
                                    <td className="text-right">
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            onClick={() => {
                                                onOpen(row);
                                            }}
                                        >
                                            {openLabel}
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
