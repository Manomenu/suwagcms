import type { ComponentProps, ReactNode } from "react";

import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { ChangeIdDialog } from "@/design/changeIdDialog";
import { HistoryPanel } from "@/design/historyPanel";
import { UsagePanel } from "@/design/usagePanel";

/**
 * What surrounds a resource's form, in the same order on every screen. `children` is the form and whatever the kind adds beside
 * it; `notice` and `panel` are the kind's own lines above the rest. A kind may hold a resource that cannot be edited: `editable` false takes Change id and
 * Delete away.
 */
export function EditorPanels({
    noun,
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
    onDelete,
    history,
    notice,
    panel,
    children,
}: {
    noun: string;
    editedId: string | null;
    editable?: boolean;
    deleteFailure: string | null;
    dirty: boolean;
    changingId: boolean;
    onChangingId: (changing: boolean) => void;
    preview: ComponentProps<typeof ChangeIdDialog>["preview"];
    apply: ComponentProps<typeof ChangeIdDialog>["apply"];
    onChanged: (newId: string) => void;
    usage: Pick<ComponentProps<typeof UsagePanel>, "usage" | "failure" | "hrefOf"> | null;
    onDelete: () => void;
    history: ComponentProps<typeof HistoryPanel>;
    notice?: ReactNode;
    panel?: ReactNode;
    children: ReactNode;
}) {
    return (
        <>
            {notice}
            {deleteFailure !== null && <Alert variant="destructive">{deleteFailure}</Alert>}
            {panel}
            {editedId !== null && editable && (
                <div className="flex items-center gap-3">
                    <Button
                        type="button"
                        variant="outline"
                        disabled={dirty}
                        onClick={() => {
                            onChangingId(true);
                        }}
                    >
                        Change id
                    </Button>
                    {dirty && <span className="text-sm text-muted-foreground">Save your changes first.</span>}
                </div>
            )}
            {changingId && editedId !== null && (
                <ChangeIdDialog
                    noun={noun}
                    id={editedId}
                    preview={preview}
                    apply={apply}
                    onChanged={onChanged}
                    onCancel={() => {
                        onChangingId(false);
                    }}
                />
            )}
            {editedId !== null && usage !== null && <UsagePanel {...usage} {...(editable ? { onDelete } : {})} />}
            {children}
            {editedId !== null && <HistoryPanel {...history} />}
        </>
    );
}
