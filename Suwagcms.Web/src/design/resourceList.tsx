import type { ReactNode } from "react";
import { ResourceList as Frame, type Paging, type ResourceColumn } from "@maniumek/suwfrontkit";

import { HistoryPanel, type HistoryPanelProps } from "@/design/page";

export function ResourceList<Row>({
    title,
    addLabel,
    addDisabled = false,
    onAdd,
    above,
    rows,
    paging,
    columns,
    idOf,
    openLabel = "Edit",
    onOpen,
    empty,
    history,
}: {
    title: string;
    addLabel: string;
    addDisabled?: boolean;
    onAdd: () => void;
    above?: ReactNode;
    rows: Row[];
    paging: Paging;
    columns: ResourceColumn<Row>[];
    idOf: (row: Row) => string;
    openLabel?: string;
    onOpen: (row: Row) => void;
    empty: ReactNode;
    history: HistoryPanelProps;
}) {
    return (
        <Frame
            title={title}
            columns={columns}
            rows={rows}
            idOf={idOf}
            openLabel={openLabel}
            link={(row, label) => (
                <button
                    type="button"
                    onClick={() => {
                        onOpen(row);
                    }}
                >
                    {label}
                </button>
            )}
            addLabel={addLabel}
            addDisabled={addDisabled}
            add={(label) => (
                <button type="button" onClick={onAdd}>
                    {label}
                </button>
            )}
            {...(rows.length === 0 ? {} : { paging })}
            empty={empty}
            above={above}
            below={<HistoryPanel {...history} />}
        />
    );
}
