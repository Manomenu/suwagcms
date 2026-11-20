import { useState, type ReactNode } from "react";

import { Button } from "@/design/button";

type Direction = "up" | "down";

interface Fold {
    label: string;
    open: boolean;
    canFold: boolean;
    title?: string | undefined;
    onToggle: () => void;
}

interface Move {
    what: string;
    can: (direction: Direction) => boolean;
    onMove: (direction: Direction) => void;
}

interface Remove {
    label: string;
    steps: number;
    onRemove: () => void;
}

export interface ListRowProps {
    kind: string;
    tag: string;
    tone: string;
    number?: number;
    name?: ReactNode | undefined;
    text?: ReactNode | undefined;
    fold?: Fold | undefined;
    note?: ReactNode | undefined;
    move?: Move | undefined;
    remove?: Remove | undefined;
    data?: Record<`data-${string}`, string>;
}

function Tag({ tone, children }: { tone: string; children: ReactNode }) {
    return <span className={`inline-flex shrink-0 rounded-md px-2 py-0.5 text-xs font-semibold ${tone}`}>{children}</span>;
}

function RemoveControl({ label, steps, onRemove }: Remove) {
    const [asking, setAsking] = useState(false);
    if (asking) {
        return (
            <span className="flex items-center gap-1 text-sm">
                {steps} {steps === 1 ? "step goes" : "steps go"} with it.
                <Button type="button" variant="destructive" size="xs" onClick={onRemove}>
                    Remove them
                </Button>
                <Button
                    type="button"
                    variant="ghost"
                    size="xs"
                    onClick={() => {
                        setAsking(false);
                    }}
                >
                    Keep
                </Button>
            </span>
        );
    }
    return (
        <Button
            type="button"
            variant="ghost"
            size="xs"
            aria-label={label}
            onClick={() => {
                if (steps === 0) onRemove();
                else setAsking(true);
            }}
        >
            ✕
        </Button>
    );
}

function chevron(fold: Fold): string {
    if (!fold.canFold) return "before:content-['']";
    return fold.open ? "before:content-['▾']" : "before:content-['▸']";
}

export function ListRow({ kind, tag, tone, number, name, text, fold, note, move, remove, data }: ListRowProps) {
    const open = fold?.open === true;
    return (
        <div
            className={`flex flex-wrap items-center gap-2 rounded-md px-1 py-1 text-sm ${open ? "bg-secondary" : ""}`}
            data-row={kind}
            {...data}
        >
            <span className="w-5 shrink-0 text-right text-xs text-muted-foreground">
                {number === undefined ? "" : `${String(number)}.`}
            </span>
            <span className="flex w-20 shrink-0">
                <Tag tone={tone}>{tag}</Tag>
            </span>
            <span className="flex min-w-0 flex-1 items-center gap-2">
                {name}
                {fold ? (
                    <button
                        type="button"
                        className={`min-w-0 flex-1 cursor-pointer truncate text-left before:inline-block before:w-4 before:text-muted-foreground disabled:cursor-default ${chevron(fold)}`}
                        aria-expanded={fold.open}
                        aria-label={fold.label}
                        title={fold.title}
                        disabled={!fold.canFold}
                        onClick={fold.onToggle}
                    >
                        {text}
                    </button>
                ) : (
                    <span className="min-w-0 flex-1 truncate pl-4">{text}</span>
                )}
                {note}
            </span>
            <span className="flex w-14 shrink-0 items-center">
                {move &&
                    (["up", "down"] as const).map((direction) => (
                        <Button
                            key={direction}
                            type="button"
                            variant="ghost"
                            size="xs"
                            aria-label={`Move ${move.what} ${direction}`}
                            disabled={!move.can(direction)}
                            onClick={() => {
                                move.onMove(direction);
                            }}
                        >
                            {direction === "up" ? "↑" : "↓"}
                        </Button>
                    ))}
            </span>
            <span className="flex min-w-7 shrink-0 justify-end">{remove && <RemoveControl {...remove} />}</span>
        </div>
    );
}
