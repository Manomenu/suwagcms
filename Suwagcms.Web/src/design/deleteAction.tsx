import { useState } from "react";

import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { Paper } from "@/design/page";

export function DeleteAction({
    noun,
    id,
    usedBy,
    failure,
    onDelete,
}: {
    noun: string;
    id: string;
    usedBy: number;
    failure: string | null;
    onDelete: () => void;
}) {
    const [asking, setAsking] = useState(false);
    return (
        <div className="flex flex-col gap-2">
            {failure !== null && <Alert variant="destructive">{failure}</Alert>}
            <div className="flex items-center gap-3">
                <Button
                    type="button"
                    variant="destructive"
                    disabled={usedBy > 0}
                    onClick={() => {
                        setAsking(true);
                    }}
                >
                    Delete
                </Button>
                {usedBy > 0 && (
                    <span className="text-sm text-muted-foreground">
                        Used in {usedBy} {usedBy === 1 ? "place" : "places"}. Remove {usedBy === 1 ? "that reference" : "those references"}{" "}
                        first.
                    </span>
                )}
            </div>
            {asking && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/70 p-4">
                    <Paper
                        role="alertdialog"
                        aria-label={`Delete ${noun}`}
                        aria-modal="true"
                        className="flex w-full max-w-lg flex-col gap-3"
                    >
                        <h2 className="text-base font-semibold wrap-anywhere">
                            Delete {noun} {id}?
                        </h2>
                        <p className="text-sm text-muted-foreground">This cannot be undone. Versions already created keep it.</p>
                        <div className="flex justify-end gap-2">
                            <Button
                                type="button"
                                variant="outline"
                                autoFocus
                                onClick={() => {
                                    setAsking(false);
                                }}
                            >
                                Keep it
                            </Button>
                            <Button
                                type="button"
                                variant="destructive"
                                onClick={() => {
                                    setAsking(false);
                                    onDelete();
                                }}
                            >
                                Delete {noun}
                            </Button>
                        </div>
                    </Paper>
                </div>
            )}
        </div>
    );
}
