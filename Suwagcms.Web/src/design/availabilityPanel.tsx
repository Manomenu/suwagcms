import { useState } from "react";

import { Alert } from "@/design/alert";
import { Badge } from "@/design/badge";
import { Button } from "@/design/button";
import { Paper } from "@/design/page";

/** One thing that uses the resource, as the "Used by" panel lists it. */
interface UsedByEntry {
    kind: string;
    id: string;
    field: string;
}

/** Whether the bot has the setting, from its own answer. */
interface Delivery {
    state: string;
    reason?: string | null;
}

/** Says so loudly when the bot does not have the setting, because off is often an emergency and nobody should believe it too early. */
export function AvailabilityNotice({
    delivery,
    failure,
    onSend,
}: {
    delivery: Delivery | null;
    failure: string | null;
    onSend: () => void;
}) {
    return (
        <>
            {failure !== null && <Alert variant="destructive">{failure}</Alert>}
            {delivery?.state === "NotDelivered" && (
                <Alert variant="warning" className="flex items-center gap-3">
                    <span className="min-w-0 flex-1">
                        <b>Not delivered.</b> The bot still has the previous setting: {delivery.reason}
                    </span>
                    <Button type="button" variant="outline" size="sm" className="shrink-0" onClick={onSend}>
                        Send again
                    </Button>
                </Alert>
            )}
        </>
    );
}

/** The state of one resource in a list. */
export function AvailabilityBadge({ off }: { off: boolean }) {
    return <Badge variant={off ? "danger" : "neutral"}>{off ? "off" : "on"}</Badge>;
}

/** Lists what uses the resource before it goes off; being in use never stops it, because no reference breaks. */
export function TurnOffDialog({
    id,
    usedBy,
    onConfirm,
    onCancel,
}: {
    id: string;
    usedBy: UsedByEntry[];
    onConfirm: () => void;
    onCancel: () => void;
}) {
    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/70 p-4">
            <Paper role="dialog" aria-label="Turn off" className="flex w-full max-w-lg flex-col gap-3">
                <h2 className="text-base font-semibold">Turn off {id}?</h2>
                {usedBy.length === 0 ? (
                    <p className="text-sm">Nothing uses it.</p>
                ) : (
                    <section aria-label="Used by" className="flex flex-col gap-1 text-sm">
                        <p className="font-medium">Used by:</p>
                        <ul className="flex flex-col gap-1">
                            {usedBy.map((entry) => (
                                <li key={`${entry.kind}:${entry.id}:${entry.field}`}>
                                    {entry.id}{" "}
                                    <span className="text-muted-foreground">
                                        {entry.kind} · {entry.field}
                                    </span>
                                </li>
                            ))}
                        </ul>
                        <p className="text-muted-foreground">They keep working.</p>
                    </section>
                )}
                <p className="text-sm text-muted-foreground">
                    It takes effect on the bot at once, also in conversations on older versions.
                </p>
                <div className="flex justify-end gap-2">
                    <Button type="button" variant="outline" onClick={onCancel}>
                        Cancel
                    </Button>
                    <Button type="button" variant="destructive" onClick={onConfirm}>
                        Turn off
                    </Button>
                </div>
            </Paper>
        </div>
    );
}

/** The switch of one resource. Turning off waits until the "Used by" list is loaded, so the question always shows it; turning on asks nothing. */
export function AvailabilityPanel({
    id,
    off,
    delivery,
    usedBy,
    onTurn,
}: {
    id: string;
    off: boolean;
    delivery: Delivery | null;
    usedBy: UsedByEntry[] | null;
    onTurn: (off: boolean) => void;
}) {
    const [asking, setAsking] = useState(false);
    return (
        <div className="flex items-center gap-3" role="group" aria-label="Availability">
            <span className="text-sm font-medium">Availability</span>
            <AvailabilityBadge off={off} />
            <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={!off && usedBy === null}
                onClick={() => {
                    if (off) onTurn(false);
                    else setAsking(true);
                }}
            >
                {off ? "Turn on" : "Turn off"}
            </Button>
            {delivery?.state === "Confirmed" && <span className="text-sm text-muted-foreground">Bot confirmed</span>}
            {asking && usedBy !== null && (
                <TurnOffDialog
                    id={id}
                    usedBy={usedBy}
                    onConfirm={() => {
                        setAsking(false);
                        onTurn(true);
                    }}
                    onCancel={() => {
                        setAsking(false);
                    }}
                />
            )}
        </div>
    );
}
