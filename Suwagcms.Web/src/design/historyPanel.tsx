import type { ComponentProps } from "react";

import { ChangeList } from "@/design/changeList";
import { Paper } from "@/design/page";

/** The shared list where a page holds more than it: only this names it. */
export function HistoryPanel({ note, ...list }: ComponentProps<typeof ChangeList> & { note?: string }) {
    return (
        <Paper aria-label="History" role="region" className="flex flex-col gap-2 wrap-anywhere">
            <h3 className="text-sm font-semibold">History</h3>
            {note !== undefined && <p className="text-sm text-muted-foreground">{note}</p>}
            <ChangeList {...list} />
        </Paper>
    );
}
