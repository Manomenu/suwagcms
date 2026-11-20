import * as React from "react";
import { Page as Frame, Paper } from "@maniumek/suwfrontkit";

import { ChangeList } from "@/design/changeList";

export type HistoryPanelProps = React.ComponentProps<typeof ChangeList> & { note?: string };

export function Page({
    title,
    actions,
    history,
    children,
}: {
    title: string;
    actions?: React.ReactNode;
    history?: HistoryPanelProps;
    children?: React.ReactNode;
}) {
    return (
        <Frame title={title} actions={actions}>
            {children}
            {history !== undefined && <HistoryPanel {...history} />}
        </Frame>
    );
}

export function HistoryPanel({ note, ...list }: HistoryPanelProps) {
    return (
        <Paper aria-label="History" role="region" className="flex flex-col gap-2 wrap-anywhere">
            <h3 className="text-sm font-semibold">History</h3>
            {note !== undefined && <p className="text-sm text-muted-foreground">{note}</p>}
            <ChangeList {...list} />
        </Paper>
    );
}
