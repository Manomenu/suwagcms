import type { ReactNode } from "react";
import { ResourceList as Frame, type Paging, type ResourceColumn } from "@maniumek/suwfrontkit";

import { HistoryPanel, type HistoryPanelProps } from "@/design/page";

export function ResourceList<Row>({
    title,
    addLabel,
    addDisabled = false,
    addHref,
    above,
    rows,
    paging,
    columns,
    idOf,
    openLabel = "Edit",
    hrefOf,
    empty,
    history,
}: {
    title: string;
    addLabel: string;
    addDisabled?: boolean;
    addHref: string;
    above?: ReactNode;
    rows: Row[];
    paging: Paging;
    columns: ResourceColumn<Row>[];
    idOf: (row: Row) => string;
    openLabel?: string;
    hrefOf: (row: Row) => string;
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
            hrefOf={hrefOf}
            addLabel={addLabel}
            addDisabled={addDisabled}
            addHref={addHref}
            {...(rows.length === 0 ? {} : { paging })}
            empty={empty}
            above={above}
            below={<HistoryPanel {...history} />}
        />
    );
}
