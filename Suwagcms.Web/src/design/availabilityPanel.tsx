import { Alert, Badge, Button, ConfirmDialog } from "@maniumek/suwfrontkit";
import { useState } from "react";

interface UsedByEntry {
    kind: string;
    id: string;
    field: string;
}

interface Delivery {
    state: string;
    reason?: string | null;
}

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

export function AvailabilityBadge({ off }: { off: boolean }) {
    return <Badge variant={off ? "danger" : "neutral"}>{off ? "off" : "on"}</Badge>;
}

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
        <ConfirmDialog
            open
            onOpenChange={(open) => {
                if (!open) onCancel();
            }}
            title={`Turn off ${id}?`}
            variant="destructive"
            confirmLabel="Turn off"
            onConfirm={onConfirm}
        >
            <div className="flex flex-col gap-3 text-sm">
                {usedBy.length === 0 ? (
                    <p>Nothing uses it.</p>
                ) : (
                    <section aria-label="Used by" className="flex flex-col gap-1">
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
                <p className="text-muted-foreground">It takes effect on the bot at once, also in conversations on older versions.</p>
            </div>
        </ConfirmDialog>
    );
}

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
