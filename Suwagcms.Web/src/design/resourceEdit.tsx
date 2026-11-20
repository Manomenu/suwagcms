import type { ComponentProps, ReactNode } from "react";
import { Button, InfoTip, ResourceEdit as Frame } from "@maniumek/suwfrontkit";

import { ChangeIdDialog } from "@/design/changeIdDialog";
import { HistoryPanel, type HistoryPanelProps } from "@/design/page";
import { UsagePanel } from "@/design/usagePanel";

export function BackToList({ plural, onBack }: { plural: string; onBack: () => void }) {
    return (
        <div>
            <Button type="button" variant="link" className="h-auto px-0" onClick={onBack}>
                ← Back to {plural}
            </Button>
        </div>
    );
}

function changeIdHelp(noun: string, idWord: string): string {
    return `Gives this ${noun} a new ${idWord} and rewrites every link and reference to it, all or nothing. You see what would change before anything does. Plain text naming it and versions already created stay as they are.`;
}

function blockedBy(usedBy: number): string | null {
    if (usedBy === 0) return null;
    return usedBy === 1
        ? "Used in 1 place. Remove that reference first."
        : `Used in ${String(usedBy)} places. Remove those references first.`;
}

export function ResourceEdit({
    noun,
    plural,
    addTitle,
    idWord = "id",
    onBack,
    ready,
    missingId,
    editedId,
    editable = true,
    deleteFailure,
    dirty,
    changingId,
    onChangingId,
    preview,
    apply,
    onChanged,
    usage,
    showUses = true,
    onDelete,
    history,
    notice,
    panel,
    children,
}: {
    noun: string;
    plural: string;
    addTitle: string;
    idWord?: string;
    onBack: () => void;
    ready: boolean;
    missingId: string | null;
    editedId: string | null;
    editable?: boolean;
    deleteFailure: string | null;
    dirty: boolean;
    changingId: boolean;
    onChangingId: (changing: boolean) => void;
    preview: ComponentProps<typeof ChangeIdDialog>["preview"];
    apply: ComponentProps<typeof ChangeIdDialog>["apply"];
    onChanged: (newId: string) => void;
    usage: Pick<ComponentProps<typeof UsagePanel>, "usage" | "failure" | "hrefOf" | "onFollow"> | null;
    showUses?: boolean;
    onDelete: () => Promise<void>;
    history: HistoryPanelProps;
    notice?: ReactNode;
    panel?: ReactNode;
    children: ReactNode;
}) {
    const canChange = editedId !== null && editable;
    const usedBy = usage === null ? 0 : (usage.usage?.usedBy.length ?? null);
    const capitalNoun = `${noun.charAt(0).toUpperCase()}${noun.slice(1)}`;
    const title = editedId === null ? addTitle : `${capitalNoun} ${editedId}`;
    return (
        <Frame
            title={title}
            width="full"
            back={(label) => (
                <button type="button" onClick={onBack}>
                    {label}
                </button>
            )}
            backLabel={`Back to ${plural}`}
            ready={ready}
            failure={
                missingId === null ? null : `This bot has no ${noun} '${missingId}'. It may have been deleted, or its ${idWord} changed.`
            }
            actions={
                canChange ? (
                    <>
                        {dirty && <span className="text-sm text-muted-foreground">Save your changes first.</span>}
                        <Button
                            type="button"
                            variant="outline"
                            disabled={dirty}
                            onClick={() => {
                                onChangingId(true);
                            }}
                        >
                            Change {idWord}
                        </Button>
                        <InfoTip about={`changing the ${idWord}`} help={changeIdHelp(noun, idWord)} />
                    </>
                ) : undefined
            }
            above={
                <>
                    {notice}
                    {panel}
                    {changingId && editedId !== null && (
                        <ChangeIdDialog
                            noun={noun}
                            idWord={idWord}
                            id={editedId}
                            preview={preview}
                            apply={apply}
                            onChanged={onChanged}
                            onCancel={() => {
                                onChangingId(false);
                            }}
                        />
                    )}
                    {editedId !== null && usage !== null && <UsagePanel {...usage} showUses={showUses} />}
                </>
            }
            {...(canChange && usedBy !== null
                ? {
                      deletion: {
                          onDelete,
                          question: `Delete ${noun} ${editedId}?`,
                          consequence: "This cannot be undone. Versions already created keep it.",
                          blocked: blockedBy(usedBy),
                          failure: deleteFailure,
                          confirmLabel: `Delete ${noun}`,
                      },
                  }
                : {})}
            below={editedId === null ? undefined : <HistoryPanel {...history} />}
        >
            {children}
        </Frame>
    );
}
