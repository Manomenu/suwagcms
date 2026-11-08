import * as React from "react";

import { ChangeList } from "@/design/changeList";
import { cn } from "@/design/classNames";

/** What a page's History panel shows: the entries the page filtered, and a note above them when the page has one. */
export type HistoryPanelProps = React.ComponentProps<typeof ChangeList> & { note?: string };

/**
 * The frame of every page: the title and its actions, the page's own content, and, on a main screen, the History panel at the bottom,
 * holding the changes of what the screen is about. The panel exists only here, so no screen can draw one of its own or put it elsewhere.
 */
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
        <div className="flex flex-col gap-4">
            <div className="flex items-center justify-between gap-4">
                <h1 className="text-[26px] leading-[1.35] font-bold">{title}</h1>
                {actions !== undefined && <div className="flex items-center gap-2">{actions}</div>}
            </div>
            {children}
            {history !== undefined && <HistoryPanel {...history} />}
        </div>
    );
}

// Why: the shared list holds no name, so the panel that holds it says "History".
function HistoryPanel({ note, ...list }: HistoryPanelProps) {
    return (
        <Paper aria-label="History" role="region" className="flex flex-col gap-2 wrap-anywhere">
            <h3 className="text-sm font-semibold">History</h3>
            {note !== undefined && <p className="text-sm text-muted-foreground">{note}</p>}
            <ChangeList {...list} />
        </Paper>
    );
}

export function Paper({ className, ...props }: React.ComponentProps<"div">) {
    return <div className={cn("rounded-lg border border-border bg-card p-4", className)} {...props} />;
}
