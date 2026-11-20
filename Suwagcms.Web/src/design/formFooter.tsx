import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { MistakeItems, type ShownMistake } from "@/design/mistakeItems";
import { StickyActions } from "@/design/stickyActions";

export function FormFooter({
    saving,
    loose,
    editing,
    reloadLabel,
    onReload,
}: {
    saving: {
        pending: boolean;
        refusal: { message: string; mistakes: ShownMistake[] } | null;
        forgetRefusal: () => void;
        cancel: () => void;
    };
    loose: ShownMistake[];
    editing: boolean;
    reloadLabel: string;
    onReload: () => void;
}) {
    const { refusal } = saving;
    const moved = refusal?.mistakes.some((mistake) => mistake.code === "resource.stale") ?? false;
    return (
        <>
            {refusal !== null && refusal.mistakes.length === 0 && <Alert variant="destructive">{refusal.message}</Alert>}
            {loose.length > 0 && (
                <Alert variant="destructive">
                    <MistakeItems mistakes={loose} />
                </Alert>
            )}
            <StickyActions>
                <Button type="submit" disabled={saving.pending}>
                    {editing ? "Save" : "Add"}
                </Button>
                {moved && (
                    <Button
                        type="button"
                        variant="outline"
                        onClick={() => {
                            saving.forgetRefusal();
                            onReload();
                        }}
                    >
                        {reloadLabel}
                    </Button>
                )}
                <Button type="button" variant="outline" onClick={saving.cancel}>
                    Cancel
                </Button>
            </StickyActions>
        </>
    );
}
