import type { ComponentProps, ReactNode } from "react";

import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { ChangeIdDialog } from "@/design/changeIdDialog";
import { DeleteAction } from "@/design/deleteAction";
import { HistoryPanel } from "@/design/historyPanel";
import { InfoTip } from "@/design/infoTip";
import { Page } from "@/design/page";
import { UsagePanel } from "@/design/usagePanel";

/** The way from a screen of one resource back to the list of them. */
export function BackToList({ plural, onBack }: { plural: string; onBack: () => void }) {
    return (
        <div>
            <Button type="button" variant="link" className="h-auto px-0" onClick={onBack}>
                ← Back to {plural}
            </Button>
        </div>
    );
}

/** Says what changing the id does, in the word a kind uses for it, beside the button. */
export function changeIdHelp(noun: string, idWord: string): string {
    return `Gives this ${noun} a new ${idWord} and rewrites every link and reference to it, all or nothing. You see what would change before anything does. Plain text naming it and versions already created stay as they are.`;
}

/**
 * The screen of one resource, and only of it, the same on every kind: a way back, the heading with its id and Change id, the kind's
 * own lines, Used by and Uses, `children` (the form and whatever the kind adds beside it), Delete and the history. `editable` false
 * takes Change id and Delete away, for a kind that may hold a resource that cannot be edited. `idWord` is what the kind calls its id.
 */
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
    onDelete: () => void;
    history: ComponentProps<typeof HistoryPanel>;
    notice?: ReactNode;
    panel?: ReactNode;
    children: ReactNode;
}) {
    const back = <BackToList plural={plural} onBack={onBack} />;
    if (missingId !== null) {
        return (
            <div className="flex flex-col gap-4">
                {back}
                <Alert variant="destructive">
                    This bot has no {noun} '{missingId}'. It may have been deleted, or its {idWord} changed.
                </Alert>
            </div>
        );
    }
    if (!ready) {
        return (
            <div className="flex flex-col gap-4">
                {back}
                <p className="text-muted-foreground">Loading…</p>
            </div>
        );
    }
    const canChange = editedId !== null && editable;
    const usedBy = usage === null ? 0 : (usage.usage?.usedBy.length ?? null);
    const capitalNoun = `${noun.charAt(0).toUpperCase()}${noun.slice(1)}`;
    return (
        <div className="flex flex-col gap-4">
            {back}
            <Page
                title={editedId === null ? addTitle : `${capitalNoun} ${editedId}`}
                {...(canChange
                    ? {
                          actions: (
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
                          ),
                      }
                    : {})}
            >
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
                {children}
                {canChange && usedBy !== null && (
                    <DeleteAction noun={noun} id={editedId} usedBy={usedBy} failure={deleteFailure} onDelete={onDelete} />
                )}
                {editedId !== null && <HistoryPanel {...history} />}
            </Page>
        </div>
    );
}
